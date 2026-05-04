/** Program.cs - Bu dosya, JWT (JSON Web Token) sisteminin kurallarını belirler.
 * Gelen anahtarların doğruluğunu, süresini ve kim tarafından verildiğini kontrol eder. */
using IHA_Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Veritabanı
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

// Swagger Servisleri
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Bu bölüm, gelen token'ların nasıl doğrulanacağını sisteme öğretir.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, // Token'ı kimin oluşturduğunu kontrol et (IhaBackend)
            ValidateAudience = true, // Token'ın kimin için oluşturulduğunu kontrol et (IhaFrontend)
            ValidateLifetime = true, // Token'ın süresinin dolup dolmadığını kontrol et
            ValidateIssuerSigningKey = true, // Güvenlik anahtarını doğrula
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero, // Tolerans süresini sıfıra indir
            // appsettings.json'daki gizli anahtarı kullanarak şifreyi çözer
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

builder.Services.AddAuthorization(); // Yetkilendirme servislerini aktif et

var app = builder.Build();

// CORS'u aktif et
app.UseCors("AllowAll");
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "IHA_Backend v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

app.UseAuthentication(); // Gelen isteğin kimlik kontrolü
app.UseAuthorization();  // Kimliği doğrulanmış kişinin yetki kontrolü

app.MapControllers();

app.Run();
