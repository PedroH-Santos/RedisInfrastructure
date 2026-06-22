using Microsoft.EntityFrameworkCore;
using RedisCacheLab.Models;

namespace CacheApi.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Teclado Mecânico", Description = "Switch azul, RGB", Price = 350.00m, Stock = 25 },
            new Product { Id = 2, Name = "Mouse Gamer", Description = "16000 DPI", Price = 180.00m, Stock = 50 },
            new Product { Id = 3, Name = "Monitor 27\"", Description = "144Hz, QHD", Price = 1500.00m, Stock = 10 }
        );
    }
}