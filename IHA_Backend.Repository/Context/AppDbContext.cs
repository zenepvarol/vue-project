using Microsoft.EntityFrameworkCore;
using IHA_Backend.Core.Entities;

namespace IHA_Backend.Repository.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Aircraft> Aircrafts { get; set; }
        public DbSet<Airport> Airports { get; set; }
        public DbSet<FlightHistory> FlightHistories { get; set; }
        public DbSet<User> Users { get; set; }
    }
}
