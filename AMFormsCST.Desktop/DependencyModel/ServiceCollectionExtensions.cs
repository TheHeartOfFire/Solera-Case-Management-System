using AMFormsCST.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AMFormsCST.Desktop.DependencyModel;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransientFromNamespace(
        this IServiceCollection services,
        string namespaceName,
        params Assembly[] assemblies
    )
    {
        foreach (Assembly assembly in assemblies)
        {
            IEnumerable<Type> types = assembly
                .GetTypes()
                .Where(x =>
                    x.IsClass
                    && !x.IsAbstract
                    && !x.IsNested
                    && !Attribute.IsDefined(x, typeof(CompilerGeneratedAttribute))
                    && x.Namespace != null
                    && x.Namespace.StartsWith(namespaceName, StringComparison.InvariantCultureIgnoreCase)
                );

            foreach (Type? type in types)
            {
                if (services.All(x => x.ServiceType != type))
                {
                    if (
                        type.Name == nameof(ViewModel)
                        && type.Namespace == typeof(ViewModel).Namespace
                    )
                    {
                        continue;
                    }

                    _ = services.AddTransient(type);
                }
            }
        }

        return services;
    }
}