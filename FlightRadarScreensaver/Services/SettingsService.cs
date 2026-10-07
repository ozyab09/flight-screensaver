using FlightRadarScreensaver.Models;
using Newtonsoft.Json;
using System.IO;

namespace FlightRadarScreensaver.Services;

public class SettingsService
{
    private readonly string _settingsPath;
    private Settings _settings = new();

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "FlightRadarScreensaver");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
        Load();
    }

    public Settings Current => _settings;

    public void Load()
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                _settings = JsonConvert.DeserializeObject<Settings>(json) ?? new Settings();
            }
            catch
            {
                _settings = new Settings();
            }
        }
        else
        {
            var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(localPath))
            {
                try
                {
                    var json = File.ReadAllText(localPath);
                    _settings = JsonConvert.DeserializeObject<Settings>(json) ?? new Settings();
                }
                catch { }
            }
        }
    }

    public void Save()
    {
        var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
        File.WriteAllText(_settingsPath, json);
    }

    public void UpdateFromCommandLine(string? city, double? lat, double? lon, int? zoom)
    {
        if (!string.IsNullOrEmpty(city) && Settings.PresetCities.TryGetValue(city, out var preset))
        {
            _settings.Latitude = preset.Lat;
            _settings.Longitude = preset.Lon;
            _settings.Zoom = preset.Zoom;
            _settings.CityName = city;
        }
        if (lat.HasValue) _settings.Latitude = lat.Value;
        if (lon.HasValue) _settings.Longitude = lon.Value;
        if (zoom.HasValue) _settings.Zoom = zoom.Value;
        Save();
    }
}