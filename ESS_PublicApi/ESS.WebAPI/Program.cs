using ESS.Application.Validators;
using ESS.Infrastructure.Polly;
using ESS.Infrastructure.Services;
using ESS.WebAPI.Middleware;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using ESS.WebAPI.Extensions;

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
    //api versioning
    builder.Services.AddApiVersioning(options =>
    {
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
        options.ReportApiVersions = true;
    });
    //rate limter
    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ip,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10
                });
        });
    });
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<ValidateCodeValidator>();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "ESS.PublicApi v1", Version = "v1" });

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
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");

    var jwtKey = jwtSettings["Key"]
        ?? throw new InvalidOperationException(
            "JWT signing key is missing from configuration.");

    var jwtIssuer = jwtSettings["Issuer"]
        ?? throw new InvalidOperationException(
            "JWT issuer is missing from configuration.");

    var jwtAudience = jwtSettings["Audience"]
        ?? throw new InvalidOperationException(
            "JWT audience is missing from configuration.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)
                ),
                RoleClaimType = ClaimTypes.Role
            };
        });

// Register HttpClient for forwarding to Private API
var baseUrl = builder.Configuration["PrivateApi:BaseUrl"];

if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var privateApiUri) ||
    (privateApiUri.Scheme != Uri.UriSchemeHttps &&
     privateApiUri.Scheme != Uri.UriSchemeHttp) ||
    !string.IsNullOrEmpty(privateApiUri.UserInfo) ||
    !string.IsNullOrEmpty(privateApiUri.Query) ||
    !string.IsNullOrEmpty(privateApiUri.Fragment))
{
    throw new InvalidOperationException(
        "PrivateApi:BaseUrl must be a valid absolute HTTP or HTTPS URL " +
        "without credentials, query parameters, or a fragment.");
}

builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri(privateApiUri, "health"),
        name: "private-api",
        failureStatus: HealthStatus.Unhealthy
    );

builder.Services.AddPrivateApiHttpClient(privateApiUri);


//OpenTelemetry
builder.Services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing
                .SetResourceBuilder(
                    ResourceBuilder.CreateDefault()
                        .AddService("ESS.PublicApi"))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter();
        });
    var app = builder.Build();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<GlobalExceptionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
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
    app.UseAuthorization();
    app.UseRateLimiter();
    app.MapControllers();
    //health checks
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";

            var result = JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    error = e.Value.Exception is null
                        ? null
                        : "Health check failed"
                })
            });

            await context.Response.WriteAsync(result);
        }
    });
    await app.RunAsync();
    await Log.CloseAndFlushAsync();
