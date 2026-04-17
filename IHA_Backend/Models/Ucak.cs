using System.ComponentModel.DataAnnotations; 

namespace IHA_Backend.Models
{
    public class Ucak
    {
        [Key]
        public int Id { get; set; }

        public string Icao24 { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Speed { get; set; }
        public double Fuel { get; set; }
        public string Status { get; set; } = "STANDBY";
    }
}