using IHA_Backend.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Veritabanı
builder.Services.AddDbContext<UygulamaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// CORS Yapılandırması (Frontend'in erişebilmesi için)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

builder.Services.AddControllers();

// 2. Swagger Servisleri
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// CORS'u aktif et
app.UseCors("AllowAll");

// 3. Swagger'ı her durumda (Development dışında da) açalım ki görebilelim
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "IHA_Backend v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.MapControllers();
app.MapGet("/", () => "Backend calisiyor. Swagger icin buraya git: /swagger");

app.Run();