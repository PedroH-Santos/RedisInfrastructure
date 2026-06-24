// ---------------------------------------------------------------
// READ-THROUGH + WRITE-THROUGH
//
// READ-THROUGH (leitura):
//   A aplicação pede ao cache. Se MISS, o próprio cache service
//   busca no banco e se popula — a aplicação nunca chama o banco
//   diretamente. Ela só fala com o ICacheService.
//   Diferença do Cache-Aside: lá a aplicação orquestra tudo
//   (verifica cache, chama banco, popula cache). Aqui esse
//   detalhe fica encapsulado dentro do serviço de cache.
//
// WRITE-THROUGH (escrita):
//   Grava no banco E no cache SINCRONAMENTE, na mesma chamada.
//   Cache e banco ficam sempre consistentes ao final da operação.
// ---------------------------------------------------------------
using RedisCacheLab.Data;
using RedisCacheLab.Models;
using RedisCacheLab.Services;

public class ReadWriteThroughService
{
    private readonly IProductRepository _repository;
    private readonly ICacheService _cache;
    private readonly ILogger<ReadWriteThroughService> _logger;

    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(2);
    public const string KeyPrefix = "rwthrough:product:";
    public const string AllKey = "rwthrough:products:all";

    public ReadWriteThroughService(
        IProductRepository repository,
        ICacheService cache,
        ILogger<ReadWriteThroughService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public static string KeyFor(int id) => $"{KeyPrefix}{id}";

    // READ-THROUGH: a aplicação chama apenas este método.
    // Ela não sabe se veio do cache ou do banco — isso é detalhe interno.
    public async Task<Product?> ReadThroughGetByIdAsync(int id)
        => await ReadThroughAsync(KeyFor(id), () => _repository.GetByIdAsync(id));

    public async Task<List<Product>> ReadThroughGetAllAsync()
        => await ReadThroughAsync(AllKey, _repository.GetAllAsync) ?? [];

    // Núcleo do Read-Through: encapsula MISS + fetch + populate.
    // A camada acima (controller) nunca toca o repositório diretamente.
    private async Task<T?> ReadThroughAsync<T>(string key, Func<Task<T?>> fetchFromDb)
    {
        var cached = await _cache.GetAsync<T>(key);
        if (cached is not null)
        {
            _logger.LogInformation("READ-THROUGH HIT: {Key}", key);
            return cached;
        }

        _logger.LogInformation("READ-THROUGH MISS: {Key} — cache service buscando no banco...", key);

        // O cache service resolve o miss por conta própria
        var data = await fetchFromDb();
        if (data is not null)
            await _cache.SetAsync(key, data, DefaultTtl);

        return data;
    }

    // WRITE-THROUGH: banco + cache na mesma chamada, sincronamente.
    public async Task<Product> WriteThroughCreateAsync(ProductDto dto)
    {
        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            Stock = dto.Stock
        };

        var created = await _repository.CreateAsync(product);

        await _cache.SetAsync(KeyFor(created.Id), created, DefaultTtl);
        await _cache.RemoveAsync(AllKey);

        _logger.LogInformation(
            "WRITE-THROUGH: produto {Id} gravado no banco e no cache na mesma operação", created.Id);

        return created;
    }

    public async Task<Product?> WriteThroughUpdateAsync(int id, ProductDto dto)
    {
        var updated = await _repository.UpdateAsync(id, dto);
        if (updated is null) return null;

        await _cache.SetAsync(KeyFor(id), updated, DefaultTtl);
        await _cache.RemoveAsync(AllKey);

        _logger.LogInformation(
            "WRITE-THROUGH: produto {Id} atualizado no banco e no cache na mesma operação", id);

        return updated;
    }

    public async Task<bool> WriteThroughDeleteAsync(int id)
    {
        var deleted = await _repository.DeleteAsync(id);
        if (deleted)
        {
            await _cache.RemoveAsync(KeyFor(id));
            await _cache.RemoveAsync(AllKey);
        }
        return deleted;
    }
}