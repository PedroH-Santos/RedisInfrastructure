using CacheApi.Services.Strategies;
using Microsoft.AspNetCore.Mvc;
using RedisCacheLab.Models;
using RedisCacheLab.Services;

namespace CacheApi.Controller;

/// <summary>
/// WRITE-AROUND
///
/// Escrita: vai DIRETO ao banco, BYPASSA o cache completamente.
/// Leitura: cache-aside normal (cache → MISS → banco → popula cache).
///
/// Como testar o comportamento:
///   1. POST /api/write-around/products  → cria no banco, cache não é tocado
///   2. GET  /api/write-around/products/{id} → MISS (não está no cache ainda)
///   3. GET  de novo → HIT (foi para o cache na leitura anterior)
///   4. PUT  /api/write-around/products/{id} → atualiza BANCO, cache fica desatualizado
///   5. GET  → ainda retorna dado antigo do cache até o TTL expirar!
///
/// Ideal para: dados escritos com frequência mas lidos raramente
/// (logs, auditoria, histórico). Evita poluir o cache com dados que
/// provavelmente não serão relidos em breve.
/// </summary>
/// 
[ApiController]
[Route("api/write-around/products")]
public class WriteAroundController : ControllerBase
{
    private readonly WriteAroundService _service;
    private readonly ICacheService _cache;

    public WriteAroundController(WriteAroundService service, ICacheService cache)
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
    /// Grava DIRETO no banco. Cache NÃO é atualizado.
    /// Primeira leitura depois desta operação sofrerá MISS.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Product>> Create(ProductDto dto)
    {
        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Atualiza DIRETO no banco. Cache NÃO é invalidado nem atualizado.
    /// Se já havia uma chave no cache, ela ficará DESATUALIZADA até o TTL expirar.
    /// Esse é o comportamento intencional do write-around — observe isso!
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<Product>> Update(int id, ProductDto dto)
    {
        var updated = await _service.UpdateAsync(id, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => await _service.DeleteAsync(id) ? NoContent() : NotFound();

    [HttpGet("cache-status/{id:int}")]
    public async Task<IActionResult> CacheStatus(int id)
    {
        var exists = await _cache.ExistsAsync(WriteAroundService.KeyFor(id));
        return Ok(new
        {
            key = WriteAroundService.KeyFor(id),
            existsInCache = exists,
            aviso = exists
                ? "Cache pode estar DESATUALIZADO se houve um PUT depois do último GET"
                : "Chave não está no cache. Próximo GET irá ao banco e populará o cache."
        });
    }

    [HttpDelete("cache-flush")]
    public async Task<IActionResult> FlushCache()
    {
        await _cache.RemoveByPrefixAsync(WriteAroundService.KeyPrefix);
        await _cache.RemoveAsync(WriteAroundService.AllKey);
        return Ok(new { message = "Cache (write-around) limpo." });
    }
}