using IHA_Backend.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Veritabanı
builder.Services.AddDbContext<UygulamaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();

// 2. Swagger Servisleri (Burası kritik)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 3. Swagger'ı her durumda (Development dışında da) açalım ki görebilelim
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    // .NET 10 bazen v1 yerine openapi/v1 kullanır, o yüzden en garantisini yazıyoruz
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "IHA_Backend v1");
    c.RoutePrefix = "swagger"; // Tarayıcıda sonuna /swagger yazınca açılır
});

app.UseHttpsRedirection();
app.MapControllers();

// Ana sayfada tıkla diye link bırakalım
app.MapGet("/", () => "Backend calisiyor. Swagger icin buraya git: /swagger");

app.Run();