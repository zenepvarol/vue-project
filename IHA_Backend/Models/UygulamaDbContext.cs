using Microsoft.EntityFrameworkCore;

namespace IHA_Backend.Models
{
    public class UygulamaDbContext : DbContext
    {
        public UygulamaDbContext(DbContextOptions<UygulamaDbContext> options) : base(options)
        {
        }

        public DbSet<Ucak> Ucaklar { get; set; }
        public DbSet<Airport> Airports { get; set; }
        public DbSet<FlightHistory> FlightHistories { get; set; }
    }
}