using CacheApi.Context;
using CacheApi.Services.Strategies;
using CacheApi.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RedisCacheLab.Data;
using RedisCacheLab.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ---- MySQL ----
var mysqlConn = builder.Configuration.GetConnectionString("MySql")
    ?? "Server=localhost;Port=3307;Database=rediscachelab;User=root;Password=root;";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(mysqlConn, ServerVersion.AutoDetect(mysqlConn)));

// ---- Redis ----
var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConn));

// ---- Repositório e cache ----
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// ---- Um serviço por estratégia de cache ----
builder.Services.AddScoped<CacheAsideService>();
builder.Services.AddScoped<WriteBehindService>();
builder.Services.AddScoped<ReadWriteThroughService>();

// ---- Fila + worker para Write-Behind ----
builder.Services.AddSingleton<IWriteBehindQueue, WriteBehindQueue>();
builder.Services.AddHostedService<WriteBehindWorker>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Redis Cache Lab API", Version = "v1" });
    c.EnableAnnotations();
});

var app = builder.Build();

// ---- Aguarda MySQL e cria/popula banco ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    const int maxRetries = 10;
    for (var i = 1; i <= maxRetries; i++)
    {
        try
        {
            db.Database.EnsureCreated();
            logger.LogInformation("MySQL pronto.");
            break;
        }
        catch (Exception ex) when (i < maxRetries)
        {
            logger.LogWarning("MySQL ainda não disponível (tentativa {I}/{Max}): {Msg}", i, maxRetries, ex.Message);
            Thread.Sleep(3000);
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Redis Cache Lab API v1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();
app.Run();