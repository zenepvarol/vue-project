using System.ComponentModel.DataAnnotations;

namespace IHA_Backend.Models
{
    public class FlightHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Icao { get; set; } = string.Empty;
        public string Departure { get; set; } = string.Empty;
        public string Arrival { get; set; } = string.Empty;
        public double Distance { get; set; }
        public DateTime FlightDate { get; set; } = DateTime.UtcNow;
    }
}
