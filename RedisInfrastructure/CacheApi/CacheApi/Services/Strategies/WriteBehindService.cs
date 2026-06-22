// ---------------------------------------------------------------
// WRITE-BEHIND (write-back)
// Escrita: atualiza cache IMEDIATAMENTE e responde ao cliente;
// a persistência real no banco acontece depois, em background.
// Ganho: latência de escrita mínima.
// Risco: janela de inconsistência e perda de dados se cair antes do flush.
// ---------------------------------------------------------------
using Microsoft.Extensions.Logging;
using RedisCacheLab.Data;
using RedisCacheLab.Models;
using RedisCacheLab.Services;

namespace CacheApi.Services.Strategies
{
    public class WriteBehindService
    {
        private readonly IProductRepository _repository;
        private readonly ICacheService _cache;
        private readonly IWriteBehindQueue _queue;
        private readonly ILogger<WriteBehindService> _logger;

        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(2);
        public const string KeyPrefix = "write-behind:product:";
        public const string AllKey = "write-behind:products:all";

        public WriteBehindService(
            IProductRepository repository,
            ICacheService cache,
            IWriteBehindQueue queue,
            ILogger<WriteBehindService> logger)
        {
            _repository = repository;
            _cache = cache;
            _queue = queue;
            _logger = logger;
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

        public async Task<Product?> UpdateAsync(int id, ProductDto dto)
        {
            // Busca produto atual (cache ou banco) para montar o objeto atualizado
            var current = await _cache.GetAsync<Product>(KeyFor(id))
                          ?? await _repository.GetByIdAsync(id);

            if (current is null) return null;

            // Atualiza o cache na hora
            current.Name = dto.Name;
            current.Description = dto.Description;
            current.Price = dto.Price;
            current.Stock = dto.Stock;
            current.UpdatedAt = DateTime.UtcNow;

            await _cache.SetAsync(KeyFor(id), current, DefaultTtl);
            await _cache.RemoveAsync(AllKey);

            // Enfileira a escrita real no banco (processada pelo WriteBehindWorker)
            await _queue.EnqueueAsync(new WriteBehindJob(id, dto, DateTime.UtcNow));

            _logger.LogInformation(
                "WRITE-BEHIND: produto {Id} atualizado no cache; banco será atualizado em background", id);

            return current;
        }
    }
}