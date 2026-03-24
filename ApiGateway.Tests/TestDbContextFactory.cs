using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MAGI.ApiGateway.Data;

namespace MAGI.ApiGateway.Tests;

/// <summary>
/// Фабрика для создания InMemory DbContext и IServiceScopeFactory в тестах.
/// </summary>
public static class TestDbContextFactory
{
    public static (IServiceScopeFactory ScopeFactory, IServiceProvider Provider) Create(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString();

        var services = new ServiceCollection();

        services.AddDbContext<MagiDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        services.AddLogging();

        var provider = services.BuildServiceProvider();

        // Ensure DB is created
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MagiDbContext>();
        db.Database.EnsureCreated();

        return (provider.GetRequiredService<IServiceScopeFactory>(), provider);
    }
}
