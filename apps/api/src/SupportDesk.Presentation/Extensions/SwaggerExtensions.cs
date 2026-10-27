using Microsoft.OpenApi;

namespace SupportDesk.Presentation.Extensions;

public static class SwaggerExtensions
{
    /// <summary>
    /// Swagger with an Authorize button, so the endpoints can still be tried from the browser now that
    /// they need a token. Sign in through <c>POST /api/auth/login</c>, then paste the token.
    /// </summary>
    public static IServiceCollection AddSwaggerWithBearerAuth(this IServiceCollection services) =>
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "The token from POST /api/auth/login. Paste the token only, without the word Bearer."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });
}
