using ESS.Application.Validators;
using ESS.Infrastructure.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using HRMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
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
    .WriteTo.File("logs/ess-public-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<ValidateCodeValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ESS.PublicApi", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,   // <-- use Http, not ApiKey
        Scheme = "Bearer",                                        // <-- Http scheme
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\""
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<HttpForwardingService>();
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
builder.Services.AddScoped<JwtService>();


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

// Register HttpClient for forwarding to Private API
var baseUrl = builder.Configuration["PrivateApi:BaseUrl"];
if (string.IsNullOrWhiteSpace(baseUrl))
{
    throw new InvalidOperationException("PrivateApi:BaseUrl is not configured.");
}

builder.Services.AddHttpClient<HttpForwardingService>(client =>
{
    client.BaseAddress = new Uri(baseUrl);
});


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
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
});
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await app.RunAsync();
await Log.CloseAndFlushAsync();
