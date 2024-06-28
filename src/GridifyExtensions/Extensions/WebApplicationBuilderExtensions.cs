using BaseConverter;
using Gridify;
using GridifyExtensions.Models;
using Microsoft.AspNetCore.Builder;
using System.Reflection;

namespace GridifyExtensions.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddGridify(this WebApplicationBuilder builder, string base36Chars,
        params Assembly[] assemblies)
    {
        PandaBaseConverter.Base36Chars = base36Chars;
        
        if (assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        AddGridify(assemblies);

        return builder;
    }

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

        QueryableExtensions.EntityGridifyMapperByType =
            assemblies.SelectMany(assembly => assembly
                    .GetTypes()
                    .Where(t => t.IsClass
                                && !t.IsAbstract
                                && t.BaseType != null
                                && t.BaseType.IsGenericType
                                && t.BaseType.GetGenericTypeDefinition() == typeof(FilterMapper<>))
                    .Select(x =>
                        new KeyValuePair<Type, object>(x.BaseType!.GetGenericArguments()[0],
                            Activator.CreateInstance(x)!)))
                .ToDictionary(x => x.Key, x => x.Value);
    }
}