using System.ComponentModel.DataAnnotations; 

namespace IHA_Backend.Models
{
    public class Ucak
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Icao24 { get; set; } = string.Empty;
        [Required]
        public string Callsign { get; set; } = string.Empty;
        [Required]
        public string ModelType { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Speed { get; set; }
        public double Fuel { get; set; }
        public string Status { get; set; } = "STANDBY";
        public bool IsSiha { get; set; } = false;
    }
}