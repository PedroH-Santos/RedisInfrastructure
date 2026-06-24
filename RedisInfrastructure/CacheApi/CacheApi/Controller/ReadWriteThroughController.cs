using Microsoft.AspNetCore.Mvc;
using RedisCacheLab.Models;
using RedisCacheLab.Services;

namespace RedisCacheLab.Controllers;

/// <summary>
/// READ-THROUGH + WRITE-THROUGH
///
/// READ-THROUGH — como testar:
///   1. GET /api/rw-through/products/{id} (primeira vez) → MISS
///      O cache service detecta o miss e busca no banco internamente.
///      O controller nunca chama o repositório diretamente.
///   2. GET de novo → HIT imediato.
///
///   Compare com Cache-Aside:
///   No Cache-Aside o CONTROLLER faz: cache → miss → chama banco → popula cache.
///   Aqui o CONTROLLER só chama o cache service — quem resolve o miss é ele.
///
/// WRITE-THROUGH — como testar:
///   1. POST /api/rw-through/products → banco + cache gravados na mesma chamada
///   2. GET  /api/rw-through/products/{id} → HIT imediato (cache já populado pelo POST)
///   3. PUT  /api/rw-through/products/{id} → banco + cache atualizados juntos
///   4. GET  de novo → HIT com valor já atualizado
///
/// Ideal para: sistemas onde consistência é crítica e leituras acontecem
/// logo após escritas (ex: perfil de usuário, dados de configuração).
/// </summary>
[ApiController]
[Route("api/rw-through/products")]
[Tags("2. Read-Through + Write-Through")]
public class ReadWriteThroughController : ControllerBase
{
    private readonly ReadWriteThroughService _service;
    private readonly ICacheService _cache;

    public ReadWriteThroughController(ReadWriteThroughService service, ICacheService cache)
    {
        _service = service;
        _cache = cache;
    }

    /// <summary>READ-THROUGH: o cache service resolve o miss internamente.</summary>
    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAll()
        => Ok(await _service.ReadThroughGetAllAsync());

    /// <summary>READ-THROUGH: controller não conhece o repositório.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetById(int id)
    {
        var product = await _service.ReadThroughGetByIdAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>WRITE-THROUGH: banco e cache gravados sincronamente.</summary>
    [HttpPost]
    public async Task<ActionResult<Product>> Create(ProductDto dto)
    {
        var created = await _service.WriteThroughCreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>WRITE-THROUGH: banco e cache atualizados na mesma operação.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<Product>> Update(int id, ProductDto dto)
    {
        var updated = await _service.WriteThroughUpdateAsync(id, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => await _service.WriteThroughDeleteAsync(id) ? NoContent() : NotFound();

    [HttpGet("cache-status/{id:int}")]
    public async Task<IActionResult> CacheStatus(int id)
    {
        var exists = await _cache.ExistsAsync(ReadWriteThroughService.KeyFor(id));
        return Ok(new
        {
            key = ReadWriteThroughService.KeyFor(id),
            existsInCache = exists,
            dica = exists
                ? "Cache populado — veio de um GET (read-through) ou POST/PUT (write-through)."
                : "Cache vazio. Faça um GET para o read-through popular, ou POST/PUT para o write-through."
        });
    }

    [HttpDelete("cache-flush")]
    public async Task<IActionResult> FlushCache()
    {
        await _cache.RemoveByPrefixAsync(ReadWriteThroughService.KeyPrefix);
        await _cache.RemoveAsync(ReadWriteThroughService.AllKey);
        return Ok(new { message = "Cache (read-through + write-through) limpo." });
    }
}