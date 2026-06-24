using RedisCacheLab.Data;
using RedisCacheLab.Models;
using RedisCacheLab.Services;

namespace CacheApi.Services.Strategies;

// ---------------------------------------------------------------
// CACHE-ASIDE (lazy loading)
// Leitura: verifica cache → se MISS, busca no banco e popula cache.
// Escrita: grava no banco → invalida as chaves do cache.
// A próxima leitura sofre MISS e repopula com dado fresco.
// ---------------------------------------------------------------
public class CacheAsideService
{
    private readonly IProductRepository _repository;
    private readonly ICacheService _cache;

    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);
    public const string KeyPrefix = "cache-aside:product:";
    public const string AllKey = "cache-aside:products:all";

    public CacheAsideService(IProductRepository repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public static string KeyFor(int id) => $"{KeyPrefix}{id}";

    public async Task<List<Product>> GetAllAsync()
    {
        var cached = await _cache.GetAsync<List<Product>>(AllKey);
        if (cached is not null) return cached;

        var products = await _repository.GetAllAsync();
        await _cache.SetAsync(AllKey, products, DefaultTtl);
        return products;
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        var cached = await _cache.GetAsync<Product>(KeyFor(id));
        if (cached is not null) return cached;

        var product = await _repository.GetByIdAsync(id);
        if (product is not null)
            await _cache.SetAsync(KeyFor(id), product, DefaultTtl);

        return product;
    }

    public async Task<Product> CreateAsync(ProductDto dto)
    {
        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            Stock = dto.Stock
        };
        var created = await _repository.CreateAsync(product);
        // Invalida a lista; a chave individual ainda não existe, então nada a remover
        await _cache.RemoveAsync(AllKey);
        return created;
    }

    public async Task<Product?> UpdateAsync(int id, ProductDto dto)
    {
        var updated = await _repository.UpdateAsync(id, dto);
        if (updated is not null)
        {
            // Invalida → próxima leitura busca no banco e repopula
            await _cache.RemoveAsync(KeyFor(id));
            await _cache.RemoveAsync(AllKey);
        }
        return updated;
    }

    public async Task<bool> DeleteAsync(int id)
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
