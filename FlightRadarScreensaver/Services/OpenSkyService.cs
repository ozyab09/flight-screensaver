using FlightRadarScreensaver.Models;
using Newtonsoft.Json;
using System.Net.Http;

namespace FlightRadarScreensaver.Services;

public class OpenSkyService
{
    /// <summary>Потолок бортов за один опрос — защита от просадки FPS.</summary>
    private const int MaxFlights = 1200;

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public OpenSkyService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "FlightRadarScreensaver/1.0");
    }

    public async Task<List<FlightData>> GetFlightsAsync(double lat, double lon, int zoom, CancellationToken ct = default)
    {
        var (minLat, maxLat, minLon, maxLon) = CalculateBoundingBox(lat, lon, zoom);

        var url = $"https://opensky-network.org/api/states/all?lamin={minLat:F4}&lomin={minLon:F4}&lamax={maxLat:F4}&lomax={maxLon:F4}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                // Область не поддерживается или превышен лимит — берём общую картину
                response = await _httpClient.GetAsync("https://opensky-network.org/api/states/all", ct);
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonConvert.DeserializeObject<OpenSkyResponse>(json);
            var flights = data?.ParseStates() ?? new List<FlightData>();

            App.Log($"OpenSky request bbox=[{minLat:F2}..{maxLat:F2}, {minLon:F2}..{maxLon:F2}] -> {flights.Count} aircraft");

            return Limit(flights, lat, lon, MaxFlights);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenSky API error: {ex.Message}");
            App.Log("OpenSky request FAILED: " + ex.Message);
            return new List<FlightData>();
        }
    }

    /// <summary>
    /// При сильном отдалении зума OpenSky возвращает тысячи бортов.
    /// Оставляем те, что ближе к центру карты: именно они попадут в кадр.
    /// Сортировать нужно по расстоянию до центра, а не по модулю координат —
    /// иначе в выборку попадают самолёты над Атлантикой вместо нужного региона.
    /// </summary>
    private static List<FlightData> Limit(List<FlightData> flights, double lat, double lon, int max)
    {
        if (flights.Count <= max) return flights;

        double lonScale = Math.Max(0.15, Math.Cos(lat * Math.PI / 180.0));

        return flights
            .OrderBy(f =>
            {
                double dLat = f.Latitude!.Value - lat;
                double dLon = (f.Longitude!.Value - lon) * lonScale;
                return dLat * dLat + dLon * dLon;
            })
            .Take(max)
            .ToList();
    }

    private static (double minLat, double maxLat, double minLon, double maxLon) CalculateBoundingBox(double lat, double lon, int zoom)
    {
        // Видимая область на 1920x1080: 360° делятся на 2^zoom по широте.
        var latSpan = 170.0 / Math.Pow(2, zoom) * 2.2;
        var lonSpan = latSpan / Math.Max(0.15, Math.Cos(lat * Math.PI / 180.0));

        return (
            Math.Max(-90, lat - latSpan),
            Math.Min(90, lat + latSpan),
            Math.Max(-180, lon - lonSpan),
            Math.Min(180, lon + lonSpan)
        );
    }
}