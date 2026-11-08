using System.Reflection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api.Common.Authorization;
using Api.Common.Extensions;
using Api.Features.Students;
using Api.Features.Courses;
using Api.Features.Admissions;
using Api.Features.Auth;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions;
using Infrastructure.Authentication;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=verticalslice.db";
var sqliteConnectionString = new SqliteConnectionStringBuilder(connectionString);
if (!Path.IsPathRooted(sqliteConnectionString.DataSource))
{
    sqliteConnectionString.DataSource = Path.Combine(
        builder.Environment.ContentRootPath,
        sqliteConnectionString.DataSource);
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(sqliteConnectionString.ConnectionString));

builder.Services.AddScoped<IAppDbContext>(provider =>
    provider.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<Application.Abstractions.IPasswordHasher, Infrastructure.Authentication.PasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<Infrastructure.BackgroundJobs.RevokedTokenCleanupService>();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.SigningKey),
        "Jwt:SigningKey is required. Set it with user-secrets.")
    .Validate(options => Encoding.UTF8.GetByteCount(options.SigningKey ?? string.Empty) >= 32,
        "Jwt:SigningKey must be at least 256 bits (32 characters).")
    .ValidateOnStart();

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["SigningKey"] ?? string.Empty)),
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<IAppDbContext>();
                var principal = context.Principal;
                var jti = principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
                var subject = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
                var issuedAtRaw = principal?.FindFirstValue(JwtRegisteredClaimNames.Iat);
                if (jti is null || !Guid.TryParse(subject, out var userId) ||
                    !long.TryParse(issuedAtRaw, out var issuedAtUnix))
                {
                    context.Fail("Required token claims are missing.");
                    return;
                }

                if (await dbContext.RevokedTokens.AnyAsync(
                    token => token.Jti == jti,
                    context.HttpContext.RequestAborted))
                {
                    context.Fail("Token has been revoked.");
                    return;
                }

                var user = await dbContext.Users.FindAsync(
                    [userId],
                    context.HttpContext.RequestAborted);
                if (user is null || !user.IsActive)
                {
                    context.Fail("User is inactive or no longer exists.");
                    return;
                }

                var issuedAtUtc = DateTimeOffset.FromUnixTimeSeconds(issuedAtUnix).UtcDateTime;
                if (user.TokensValidFromUtc is { } validFrom && issuedAtUtc < validFrom)
                {
                    context.Fail("Token was issued before the user's last sign-out-everywhere.");
                }
            }
        };
    });
builder.Services.AddAppAuthorization();
builder.Services.AddSingleton<IAuthorizationHandler, AdmissionOwnerHandler>();

// Auto-register original Lab 07 handlers
var appAssembly = typeof(IAppDbContext).Assembly;
foreach (var type in appAssembly.GetTypes().Where(t => !t.IsAbstract && !t.IsInterface))
{
    foreach (var iface in type.GetInterfaces())
    {
        if (iface.IsGenericType && (
            iface.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
            iface.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)))
        {
            builder.Services.AddScoped(iface, type);
        }
    }
}

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the access token from /api/auth/login"
        };

        document.Security ??= [];
        document.Security.Add(new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = []
        });

        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    if (response.StatusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
    {
        response.ContentType = "application/problem+json";
        await response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = response.StatusCode,
            Title = response.StatusCode == StatusCodes.Status401Unauthorized ? "Unauthorized" : "Forbidden",
            Detail = response.StatusCode == StatusCodes.Status401Unauthorized
                ? "Authentication is required to access this resource."
                : "You do not have permission to perform this action."
        });
    }
});

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapStudentEndpoints();
app.MapCourseEndpoints();
app.MapAdmissionEndpoints();
app.MapAuthEndpoints();

app.Run();
