/** Program.cs - Bu dosya, JWT (JSON Web Token) sisteminin kurallarını belirler.
 * Gelen anahtarların doğruluğunu, süresini ve kim tarafından verildiğini kontrol eder. */
using System.Text;
using IHA_Backend.Repository.Context;
using IHA_Backend.Repository.Interfaces;
using IHA_Backend.Repository.Repositories;
using IHA_Backend.Business.Interfaces;
using IHA_Backend.Business.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Veritabanı
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Business Servisleri (DI - Dependency Injection)
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IAirportService, AirportService>();
builder.Services.AddScoped<IAircraftService, AircraftService>();
builder.Services.AddScoped<IFlightHistoryService, FlightHistoryService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<ITelemetryService, TelemetryService>(); // Telemetri Servisi (Uygulama boyunca bellekte tek bir liste tutar)
builder.Services.AddSingleton<IHA_Backend.Core.Interfaces.INotificationService, IHA_Backend.Services.SignalRNotificationService>(); // SignalR Bildirim Servisi kaydı
builder.Services.AddSignalR(); // Gerçek zamanlı bildirimler için SignalR servisini ekle

// CORS Yapılandırması (Frontend'in erişebilmesi için)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.SetIsOriginAllowed(origin => true) // SignalR'da credentials (kimlik bilgisi) iletimi için dinamik origin izin verilir
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
});

builder.Services.AddControllers();

// Swagger Servisleri
builder.Services.AddEndpointsApiExplorer();

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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };

        // SignalR WebSockets/SSE bağlantıları için query string'deki access_token'ı ayıkla
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ASP.NET Core Web API",
        Version = "v1",
        Description = "ASP.NET Core Web API with JWT authentication. " +
        "Target Framework is .NET 10. " +
        "Swashbuckle.AspNetCore 10.1.7 is used."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Please enter token"
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
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
app.MapHub<IHA_Backend.Hubs.NotificationHub>("/hubs/notification"); // SignalR hub yolunu haritala

app.Run();
