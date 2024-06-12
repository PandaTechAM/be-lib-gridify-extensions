﻿using Gridify;
using Microsoft.AspNetCore.Builder;
using System.Reflection;

namespace GridifyExtensions;

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddGridify(this WebApplicationBuilder builder, params Assembly[] assemblies)
    {
        GridifyGlobalConfiguration.EnableEntityFrameworkCompatibilityLayer();

        QueryableExtensions.EntityGridifyMapperByType =
                                         assemblies.SelectMany(assembly => assembly
                                                   .GetTypes()
                                                   .Where(t => t.IsClass
                                                           && !t.IsAbstract
                                                            && t.BaseType != null
                                                            && t.BaseType.IsGenericType
                                                            && t.BaseType.GetGenericTypeDefinition() == typeof(GridifyMapper<>))
                                                   .Select(x => new KeyValuePair<Type, object>(x.BaseType!.GetGenericArguments()[0], Activator.CreateInstance(x)!)))
                                                   .ToDictionary(x => x.Key, x => x.Value);

        return builder;
    }
}
