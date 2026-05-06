using System.ComponentModel.DataAnnotations;

namespace IHA_Backend.Core.Entities
{
    public class Airport
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lon { get; set; }
        public string Type { get; set; } = string.Empty;
    }
}
