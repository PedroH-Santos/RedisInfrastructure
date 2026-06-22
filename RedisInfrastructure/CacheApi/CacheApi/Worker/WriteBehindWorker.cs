using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedisCacheLab.Data;
using RedisCacheLab.Services;

namespace CacheApi.Worker;

/// <summary>
/// Consome a fila de write-behind em background, simulando um delay de "lote"
/// (batch) antes de persistir no banco — assim dá pra observar, nos logs,
/// que a resposta ao cliente já voltou muito antes da escrita real acontecer.
/// </summary>
public class WriteBehindWorker : BackgroundService
{
    private readonly IWriteBehindQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WriteBehindWorker> _logger;
    private static readonly TimeSpan FlushDelay = TimeSpan.FromSeconds(3);

    public WriteBehindWorker(IWriteBehindQueue queue, IServiceScopeFactory scopeFactory, ILogger<WriteBehindWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            _logger.LogInformation("WRITE-BEHIND: job recebido para produto {Id}, aguardando {Delay}s antes de persistir...",
                job.ProductId, FlushDelay.TotalSeconds);

            await Task.Delay(FlushDelay, stoppingToken);

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IProductRepository>();

            var updated = await repository.UpdateAsync(job.ProductId, job.Dto);

            _logger.LogInformation("WRITE-BEHIND: produto {Id} persistido no banco em {Now:HH:mm:ss} (enfileirado às {EnqueuedAt:HH:mm:ss})",
                job.ProductId, DateTime.UtcNow, job.EnqueuedAt);
        }
    }
}
