namespace FlightRadarScreensaver.Models;

public class FlightData
{
    public string Icao24 { get; set; } = "";
    public string Callsign { get; set; } = "";
    public string OriginCountry { get; set; } = "";
    public long TimePosition { get; set; }
    public long LastContact { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public double? BaroAltitude { get; set; }
    public bool OnGround { get; set; }
    public double? Velocity { get; set; }
    public double? TrueTrack { get; set; }
    public double? VerticalRate { get; set; }
    public int? GeoAltitude { get; set; }
    public string? Squawk { get; set; }
    public bool Spi { get; set; }
    public int PositionSource { get; set; }

    public string DisplayCallsign => string.IsNullOrWhiteSpace(Callsign) ? Icao24 : Callsign.Trim();
    public string DisplayAltitude => BaroAltitude.HasValue ? $"{BaroAltitude.Value:F0} ft" : "—";
    public string DisplaySpeed => Velocity.HasValue ? $"{Velocity.Value * 3.6:F0} km/h" : "—";
    public string DisplayTrack => TrueTrack.HasValue ? $"{TrueTrack.Value:F0}°" : "—";
}

public class OpenSkyResponse
{
    public long Time { get; set; }
    public List<List<object>> States { get; set; } = new();

    public List<FlightData> ParseStates()
    {
        var flights = new List<FlightData>();
        foreach (var state in States)
        {
            if (state.Count < 17) continue;
            
            var flight = new FlightData
            {
                Icao24 = state[0]?.ToString() ?? "",
                Callsign = state[1]?.ToString()?.Trim() ?? "",
                OriginCountry = state[2]?.ToString() ?? "",
                TimePosition = Convert.ToInt64(state[3] ?? 0),
                LastContact = Convert.ToInt64(state[4] ?? 0),
                Longitude = state[5] as double?,
                Latitude = state[6] as double?,
                BaroAltitude = state[7] as double?,
                OnGround = state[8] as bool? ?? false,
                Velocity = state[9] as double?,
                TrueTrack = state[10] as double?,
                VerticalRate = state[11] as double?,
                GeoAltitude = state[13] as int?,
                Squawk = state[14]?.ToString(),
                Spi = state[15] as bool? ?? false,
                PositionSource = state[16] as int? ?? 0
            };

            if (flight.Latitude.HasValue && flight.Longitude.HasValue)
                flights.Add(flight);
        }
        return flights;
    }
}