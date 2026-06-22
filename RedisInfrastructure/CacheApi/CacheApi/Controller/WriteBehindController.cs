using CacheApi.Services.Strategies;
using Microsoft.AspNetCore.Mvc;
using RedisCacheLab.Models;
using RedisCacheLab.Services;

namespace CacheApi.Controller;

/// <summary>
/// WRITE-BEHIND (write-back)
///
/// Escrita: atualiza o cache IMEDIATAMENTE e responde ao cliente sem esperar o banco.
/// A persistência real no MySQL acontece depois, em background (WriteBehindWorker).
///
/// Observe nos logs do container:
///   - Resposta volta rápida (sem latência do banco)
///   - ~3 segundos depois aparece o log do worker gravando no MySQL
///
/// Ideal para: alta taxa de escritas onde a latência importa mais
/// do que a consistência imediata com o banco.
/// Risco: dados podem ser perdidos se a aplicação cair antes do flush.
/// </summary>
[ApiController]
[Route("api/write-behind/products")]
public class WriteBehindController : ControllerBase
{
    private readonly WriteBehindService _service;
    private readonly ICacheService _cache;

    public WriteBehindController(WriteBehindService service, ICacheService cache)
    {
        _service = service;
        _cache = cache;
    }

    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetById(int id)
    {
        var product = await _service.GetByIdAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>
    /// Atualiza no cache agora; banco é atualizado depois pelo worker em background.
    /// Acompanhe os logs com: docker compose logs -f api
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<Product>> Update(int id, ProductDto dto)
    {
        var updated = await _service.UpdateAsync(id, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpGet("cache-status/{id:int}")]
    public async Task<IActionResult> CacheStatus(int id)
    {
        var exists = await _cache.ExistsAsync(WriteBehindService.KeyFor(id));
        return Ok(new { key = WriteBehindService.KeyFor(id), existsInCache = exists });
    }

    [HttpDelete("cache-flush")]
    public async Task<IActionResult> FlushCache()
    {
        await _cache.RemoveByPrefixAsync(WriteBehindService.KeyPrefix);
        await _cache.RemoveAsync(WriteBehindService.AllKey);
        return Ok(new { message = "Cache (write-behind) limpo." });
    }
}