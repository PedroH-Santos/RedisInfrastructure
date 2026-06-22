using CacheApi.Context;
using Microsoft.EntityFrameworkCore;
using RedisCacheLab.Models;

namespace RedisCacheLab.Data;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id);
    Task<List<Product>> GetAllAsync();
    Task<Product> CreateAsync(Product product);
    Task<Product?> UpdateAsync(int id, ProductDto dto);
    Task<bool> DeleteAsync(int id);
}

/// <summary>
/// Repositório "lento" de propósito (Task.Delay simula latência real de disco/rede)
/// para deixar nítido, nos logs e nos tempos de resposta, o ganho de usar cache.
/// </summary>
public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;
    private static readonly TimeSpan SimulatedLatency = TimeSpan.FromMilliseconds(400);

    public ProductRepository(AppDbContext db) => _db = db;

    public async Task<Product?> GetByIdAsync(int id)
    {
        await Task.Delay(SimulatedLatency);
        return await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Product>> GetAllAsync()
    {
        await Task.Delay(SimulatedLatency);
        return await _db.Products.AsNoTracking().ToListAsync();
    }

    public async Task<Product> CreateAsync(Product product)
    {
        await Task.Delay(SimulatedLatency);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<Product?> UpdateAsync(int id, ProductDto dto)
    {
        await Task.Delay(SimulatedLatency);
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return null;

        product.Name = dto.Name;
        product.Description = dto.Description;
        product.Price = dto.Price;
        product.Stock = dto.Stock;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await Task.Delay(SimulatedLatency);
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return false;

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return true;
    }
}
