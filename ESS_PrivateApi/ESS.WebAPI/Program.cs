using ESS.Application.UseCases.ESS_SOFT_TOKENS;
using ESS.Domain.Abstractions;
using ESS.Infrastructure.Persistence;
using ESS.Infrastructure.Repositories;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
// Configure logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProcessId()
    .Enrich.WithEnvironmentUserName()
    .WriteTo.Console()
    .WriteTo.File("logs/ess_private-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();
// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<ValidateCodeValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ESS.PrivateApi", Version = "v1" });
});

// Connection string:
var oracleCon = builder.Configuration.GetConnectionString("OracleDb");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseOracle(oracleCon));

//jwt validation
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["Key"]!)
            )
        };
    });

// DI registrations
builder.Services.AddScoped<IVendorRepository, OracleVendorRepository>();
builder.Services.AddScoped<IValidateCodeRepository, OracleTokenRepository>();
builder.Services.AddScoped<ValidateCode>();

var app = builder.Build();
// Middleware to enrich logs with user information
app.Use(async (context, next) =>
{
    var user = context.User?.Identity?.IsAuthenticated == true
        ? context.User.Identity.Name
        : "Anonymous";

    using (Serilog.Context.LogContext.PushProperty("UserName", user))
    using (Serilog.Context.LogContext.PushProperty("RequestPath", context.Request.Path))
    {
        await next();
    }
});
app.UseMiddleware<ESS.WebAPI.Middleware.GlobalExceptionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication(); // validate JWT
app.UseAuthorization();//check roles and claims
app.MapControllers();
await app.RunAsync();
await Log.CloseAndFlushAsync();