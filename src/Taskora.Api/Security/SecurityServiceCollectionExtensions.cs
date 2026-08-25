using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TodoApp.Application.Abstractions;

namespace TodoApp.Api.Security;

/// <summary>
/// Registers authentication and authorization services for the API. Chooses
/// between a lightweight header/bearer-token based development scheme and
/// full JWT bearer validation depending on environment and configuration.
/// </summary>
internal static class SecurityServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ICurrentUser"/>, an authentication scheme, and
    /// authorization services. Uses <see cref="DevelopmentAuthenticationHandler"/>
    /// (which trusts an X-User-Id header or a bearer token containing a raw
    /// user id) when running in Development/Testing or when
    /// <see cref="UsesAppTokenAuthentication"/> indicates app-token mode is
    /// configured; otherwise configures standard JWT bearer authentication
    /// against the configured authority/audience.
    /// </summary>
    public static IServiceCollection AddTodoSecurity(
        this IServiceCollection services,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        if (environment.IsDevelopment() ||
            environment.IsEnvironment("Testing") ||
            UsesAppTokenAuthentication(configuration))
        {
            services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions,
                    DevelopmentAuthenticationHandler>(
                    DevelopmentAuthenticationHandler.SchemeName,
                    _ => { });
        }
        else
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = configuration["Authentication:Authority"];
                    options.Audience = configuration["Authentication:Audience"];
                    options.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true
                        };
                });
        }

        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// Determines whether the app should use the simplified app-token
    /// authentication scheme instead of JWT bearer validation. True when
    /// "Authentication:Mode" is explicitly "AppToken", or when no JWT
    /// authority is configured (missing, "local", or "app").
    /// </summary>
    public static bool UsesAppTokenAuthentication(IConfiguration configuration)
    {
        var mode = configuration["Authentication:Mode"];
        var authority = configuration["Authentication:Authority"];
        if (mode?.Equals("Jwt", StringComparison.OrdinalIgnoreCase) == true)
        {
            return false;
        }

        return mode?.Equals("AppToken", StringComparison.OrdinalIgnoreCase) == true ||
            string.IsNullOrWhiteSpace(authority) ||
            authority.Equals("local", StringComparison.OrdinalIgnoreCase) ||
            authority.Equals("app", StringComparison.OrdinalIgnoreCase);
    }
}
