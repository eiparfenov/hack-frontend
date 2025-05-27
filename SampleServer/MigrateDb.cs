using Microsoft.EntityFrameworkCore;

namespace SampleServer;

public class MigrateDb<TDbContext>: IHostedService where TDbContext: DbContext
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<MigrateDb<TDbContext>> _logger;

    public MigrateDb(IServiceScopeFactory serviceScopeFactory, ILogger<MigrateDb<TDbContext>> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("DB {dbName} migrated", typeof(TDbContext).Name);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}