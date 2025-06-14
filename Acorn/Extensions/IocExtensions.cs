using System.Reflection;
using Acorn.Database.Models;
using Acorn.Database.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace Acorn.Extensions;

internal static class IocRegistrations
{
    public static IServiceCollection AddAllOfType(this IServiceCollection services, Type type)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var handlers = assembly.GetTypes()
            .Where(t => (!type.IsGenericType && t.GetInterfaces().Any(x => x == type)) ||
                        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == type))
            .ToList();

        foreach (var handler in handlers)
        {
            var interfaceType = handler.GetInterfaces()
                .First(i => !i.IsGenericType || (i.IsGenericType && i.GetGenericTypeDefinition() == type));
            services.AddTransient(interfaceType, handler);
        }

        return services;
    }

    public static IServiceCollection AddAllOfType<T>(this IServiceCollection services)
    {
        return services.AddAllOfType(typeof(T));
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        return services
            .AddSingleton<IDbRepository<Account>, AccountRepository>()
            .AddSingleton<IDbRepository<Character>, CharacterRepository>()
            .AddSingleton<IDataFileRepository, DataFileRepository>();
    }
}

public delegate DateTime UtcNowDelegate();