using Microsoft.Extensions.DependencyInjection;

namespace DaJet.Scripting.Host
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDaJetScripting(this IServiceCollection services)
        {
            ScriptHost host = new();

            host.InitializeFromFiles();

            services.AddSingleton(host);

            return services;
        }
    }
}