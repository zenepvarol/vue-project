using Microsoft.EntityFrameworkCore;
using IHA_Backend.Core.Entities;

namespace IHA_Backend.Repository.Context
{
    public class UygulamaDbContext : DbContext
    {
        public UygulamaDbContext(DbContextOptions<UygulamaDbContext> options) : base(options)
        {
        }

        public DbSet<Ucak> Ucaklar { get; set; }
        public DbSet<Airport> Airports { get; set; }
        public DbSet<FlightHistory> FlightHistories { get; set; }
        public DbSet<User> Users { get; set; }
    }
}
