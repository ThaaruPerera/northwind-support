using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Authentication;

namespace SupportDesk.Presentation.Extensions;

public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Binds the <c>Jwt</c> section once and validates it, so a missing key or a silly lifetime stops
    /// the API at startup, then makes the API accept the tokens it issues.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("Jwt").Get<JwtSettings>()
            ?? throw new InvalidOperationException("The Jwt configuration section is missing.");

        settings.Validate();

        services.AddSingleton(settings);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearer>();

        // The fallback policy applies to every endpoint that does not say otherwise, so signing in is
        // the rule and being open is the exception: only [AllowAnonymous] endpoints, such as login,
        // are reachable without a token, and an endpoint added later is protected without anyone
        // having to remember to protect it.
        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    /// <summary>What counts as a valid token: ours, for this app, signed with our key and not expired.</summary>
    private sealed class ConfigureJwtBearer(JwtSettings settings) : IConfigureNamedOptions<JwtBearerOptions>
    {
        public void Configure(string? name, JwtBearerOptions options)
        {
            // Keep the claim names the token was issued with ("sub", "name"), not .NET's long URIs.
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = settings.Issuer,
                ValidateAudience = true,
                ValidAudience = settings.Audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                NameClaimType = "name",
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            options.Events = new JwtBearerEvents { OnChallenge = WriteUnauthorizedAsync };
        }

        public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);

        /// <summary>Answers a request with no valid token in the same problem format as every other error.</summary>
        private static Task WriteUnauthorizedAsync(JwtBearerChallengeContext context)
        {
            context.HandleResponse();

            var problem = new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Sign in to continue.",
                Status = StatusCodes.Status401Unauthorized
            };
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;

            return context.Response.WriteAsJsonAsync(problem, context.HttpContext.RequestAborted);
        }
    }
}
