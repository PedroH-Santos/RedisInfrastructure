namespace RedisCacheLab.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public record ProductDto(string Name, string Description, decimal Price, int Stock);
