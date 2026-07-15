using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TodoApp.Application.Abstractions;

namespace TodoApp.Api.Security;

internal static class SecurityServiceCollectionExtensions
{
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
