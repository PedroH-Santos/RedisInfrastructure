using CacheApi.Services.Strategies;
using Microsoft.AspNetCore.Mvc;
using RedisCacheLab.Models;
using RedisCacheLab.Services;

namespace CacheApi.Controller;

/// <summary>
/// CACHE-ASIDE (lazy loading)
///
/// Leitura: verifica o cache → MISS → vai ao banco → popula o cache com TTL.
/// Escrita: grava no banco → invalida as chaves do cache.
/// A próxima leitura sofre MISS e busca o dado atualizado direto do banco.
///
/// Ideal para: dados lidos com frequência e que podem tolerar
/// uma leitura "fria" (mais lenta) logo após uma escrita.
/// </summary>
/// 
[ApiController]
[Route("api/cache-aside/products")]
public class CacheAsideController : ControllerBase
{
    private readonly CacheAsideService _service;
    private readonly ICacheService _cache;

    public CacheAsideController(CacheAsideService service, ICacheService cache)
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

    [HttpPost]
    public async Task<ActionResult<Product>> Create(ProductDto dto)
    {
        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Product>> Update(int id, ProductDto dto)
    {
        var updated = await _service.UpdateAsync(id, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => await _service.DeleteAsync(id) ? NoContent() : NotFound();

    /// <summary>Verifica se uma chave específica existe no cache (sem ler o valor).</summary>
    [HttpGet("cache-status/{id:int}")]
    public async Task<IActionResult> CacheStatus(int id)
    {
        var exists = await _cache.ExistsAsync(CacheAsideService.KeyFor(id));
        return Ok(new { key = CacheAsideService.KeyFor(id), existsInCache = exists });
    }

    /// <summary>Limpa manualmente todas as chaves desta estratégia no Redis.</summary>
    [HttpDelete("cache-flush")]
    public async Task<IActionResult> FlushCache()
    {
        await _cache.RemoveByPrefixAsync(CacheAsideService.KeyPrefix);
        await _cache.RemoveAsync(CacheAsideService.AllKey);
        return Ok(new { message = "Cache (cache-aside) limpo." });
    }
}