using System;

namespace IHA_Backend.Core.DTOs
{
    /// <summary>
    /// Uçakların havada uçarken gönderdiği canlı telemetri verilerini taşıyan paket.
    /// Bu veri sadece RAM'de (geçici hafızada) tutulur, veritabanına yazılmaz.
    /// </summary>
    public class TelemetryDataDto
    {
        public string Icao { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lon { get; set; }
        public double Velocity { get; set; }
        public double Energy { get; set; }
        public double Altitude { get; set; }
        public double Heading { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Callsign { get; set; } = string.Empty;
    }
}
