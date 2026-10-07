namespace FlightRadarScreensaver.Models;

public class Settings
{
    public double Latitude { get; set; } = 55.7558;
    public double Longitude { get; set; } = 37.6176;
    public int Zoom { get; set; } = 8;
    public string CityName { get; set; } = "Москва";
    public int UpdateIntervalSeconds { get; set; } = 30;
    public bool ShowLabels { get; set; } = true;
    public bool ShowTrails { get; set; } = false;
    public string MapStyle { get; set; } = "osm";

    public static readonly Dictionary<string, (double Lat, double Lon, int Zoom)> PresetCities = new()
    {
        ["Москва"] = (55.7558, 37.6176, 9),
        ["Санкт-Петербург"] = (59.9343, 30.3351, 9),
        ["Новосибирск"] = (55.0084, 82.9357, 9),
        ["Екатеринбург"] = (56.8389, 60.6057, 9),
        ["Казань"] = (55.7887, 49.1221, 9),
        ["Нью-Йорк"] = (40.7128, -74.0060, 8),
        ["Лондон"] = (51.5074, -0.1278, 8),
        ["Токио"] = (35.6762, 139.6503, 8),
        ["Дубай"] = (25.2048, 55.2708, 8),
        ["Сидней"] = (-33.8688, 151.2093, 8),
    };
}