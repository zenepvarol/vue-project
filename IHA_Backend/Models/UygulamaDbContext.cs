using Microsoft.EntityFrameworkCore;

namespace IHA_Backend.Models
{
    public class UygulamaDbContext : DbContext
    {
        public UygulamaDbContext(DbContextOptions<UygulamaDbContext> options) : base(options)
        {
        }

        // Bu satır veritabanında "Ucaklar" adında bir tablo oluşturur
        public DbSet<Ucak> Ucaklar { get; set; }
    }
}