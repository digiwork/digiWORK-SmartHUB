using CompanyDirectory.Infrastructure.Ldap;
using CompanyDirectory.Shared.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyDirectory.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLdapServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LdapSettings>(
            configuration.GetSection(LdapSettings.SectionName));

        services.AddScoped<ILdapService, LdapService>();

        return services;
    }
}
