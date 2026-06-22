using System.Threading.Channels;
using RedisCacheLab.Models;

namespace RedisCacheLab.Services;

public record WriteBehindJob(int ProductId, ProductDto Dto, DateTime EnqueuedAt);

/// <summary>
/// Fila em memória (Channel) que recebe as escritas feitas no cache
/// para serem persistidas no banco de forma assíncrona/posterior (Write-Behind).
/// Em produção isso normalmente seria um Redis Stream, Kafka ou RabbitMQ —
/// aqui usamos Channel para manter o exemplo simples e focado no conceito.
/// </summary>
public interface IWriteBehindQueue
{
    ValueTask EnqueueAsync(WriteBehindJob job);
    IAsyncEnumerable<WriteBehindJob> ReadAllAsync(CancellationToken ct);
}

public class WriteBehindQueue : IWriteBehindQueue
{
    private readonly Channel<WriteBehindJob> _channel = Channel.CreateUnbounded<WriteBehindJob>();

    public ValueTask EnqueueAsync(WriteBehindJob job) => _channel.Writer.WriteAsync(job);

    public IAsyncEnumerable<WriteBehindJob> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
