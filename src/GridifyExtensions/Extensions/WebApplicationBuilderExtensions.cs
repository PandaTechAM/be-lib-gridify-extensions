using System.Collections.Frozen;
using System.Reflection;
using Gridify;
using GridifyExtensions.Models;
using GridifyExtensions.Operators;
using Microsoft.AspNetCore.Builder;

namespace GridifyExtensions.Extensions;

/// <summary>
///     Registration extensions that discover FilterMapper instances and configure Gridify globally.
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    ///     Discover all FilterMapper instances via reflection and apply the global Gridify configuration.
    ///     Scans the given assemblies, or the calling assembly when none are provided.
    /// </summary>
    public static WebApplicationBuilder AddGridify(this WebApplicationBuilder builder, params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        AddGridify(assemblies);

        return builder;
    }

    private static void AddGridify(Assembly[] assemblies)
    {
        GridifyGlobalConfiguration.EnableEntityFrameworkCompatibilityLayer();
        GridifyGlobalConfiguration.CaseInsensitiveFiltering = true;
        GridifyGlobalConfiguration.CustomOperators.Register<FlagOperator>();
        GridifyGlobalConfiguration.CaseSensitiveMapper = false;

        QueryableExtensions.EntityGridifyMapperByType =
            assemblies.SelectMany(assembly => assembly
                    .GetTypes()
                    .Where(t => t.IsClass
                                && !t.IsAbstract
                                && t.BaseType != null
                                && t.BaseType.IsGenericType
                                && t.BaseType.GetGenericTypeDefinition() ==
                                typeof(FilterMapper<>))
                    .Select(x =>
                        new KeyValuePair<Type, object>(x.BaseType!.GetGenericArguments()[0],
                            Activator.CreateInstance(x)!)))
                .ToFrozenDictionary(x => x.Key, x => x.Value);
    }
}
