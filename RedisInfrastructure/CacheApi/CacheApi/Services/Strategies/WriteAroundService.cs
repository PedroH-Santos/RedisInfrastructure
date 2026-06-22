// ---------------------------------------------------------------
// WRITE-AROUND
// Escrita: vai DIRETO ao banco, IGNORANDO o cache.
// Leitura: cache-aside normal (cache → MISS → banco → popula cache).
// Uso ideal: dados escritos uma vez e raramente relidos (logs, históricos).
// Evita poluir o cache com dados que provavelmente não serão lidos em breve.
// ---------------------------------------------------------------
using Microsoft.Extensions.Logging;
using RedisCacheLab.Data;
using RedisCacheLab.Models;
using RedisCacheLab.Services;

namespace CacheApi.Services.Strategies
{
    public class WriteAroundService
    {
        private readonly IProductRepository _repository;
        private readonly ICacheService _cache;
        private readonly ILogger<WriteAroundService> _logger;

        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(2);
        public const string KeyPrefix = "write-around:product:";
        public const string AllKey = "write-around:products:all";

        public WriteAroundService(
            IProductRepository repository,
            ICacheService cache,
            ILogger<WriteAroundService> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public static string KeyFor(int id) => $"{KeyPrefix}{id}";

        // Leitura usa cache-aside normalmente
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

        // Escrita vai direto ao banco — cache NÃO é atualizado nem invalidado
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

            _logger.LogInformation(
                "WRITE-AROUND: produto {Id} gravado no banco; cache NÃO foi tocado", created.Id);

            return created;
        }

        public async Task<Product?> UpdateAsync(int id, ProductDto dto)
        {
            var updated = await _repository.UpdateAsync(id, dto);

            _logger.LogInformation(
                "WRITE-AROUND: produto {Id} atualizado no banco; cache NÃO foi tocado " +
                "(ficará desatualizado até o TTL expirar ou a chave ser acessada novamente via GET)", id);

            return updated;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteAsync(id);

            // Delete é a única exceção: precisamos remover do cache para não retornar dado inexistente
            if (deleted)
            {
                await _cache.RemoveAsync(KeyFor(id));
                await _cache.RemoveAsync(AllKey);
                _logger.LogInformation(
                    "WRITE-AROUND: produto {Id} deletado do banco e removido do cache", id);
            }

            return deleted;
        }
    }
}