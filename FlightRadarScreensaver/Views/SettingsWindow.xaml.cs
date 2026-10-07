using FlightRadarScreensaver.Models;
using FlightRadarScreensaver.Services;
using System.Globalization;
using System.Windows;

namespace FlightRadarScreensaver.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private Settings _draft;

    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _draft = Clone(settingsService.Current);
        DataContext = _draft;

        var appIcon = App.LoadAppIcon();
        if (appIcon != null) Icon = appIcon;

        // Точка карты задаётся только вручную, поэтому название города
        // из прошлых настроек сбрасываем — иначе в шапке была бы чужая подпись.
        _draft.CityName = "";

        ZoomSlider.ValueChanged += (_, _) => UpdateZoomLabel();
        UpdateZoomLabel();
    }

    private static Settings Clone(Settings s) => new()
    {
        Latitude = s.Latitude,
        Longitude = s.Longitude,
        Zoom = s.Zoom,
        CityName = s.CityName,
        UpdateIntervalSeconds = s.UpdateIntervalSeconds,
        ShowLabels = s.ShowLabels,
        ShowTrails = s.ShowTrails,
        MapStyle = s.MapStyle
    };

    private void UpdateZoomLabel() => ZoomValueText.Text = $"Zoom {_draft.Zoom}";

    /// <summary>
    /// Разбирает координату: допускаем и точку, и запятую как разделитель дробной части.
    /// </summary>
    private static bool TryParseCoordinate(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var normalized = text.Trim().Replace(',', '.').Replace(" ", string.Empty);

        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private bool TryReadCoordinates(out string? error)
    {
        error = null;

        if (!TryParseCoordinate(LonTextBox.Text, out var lon))
        {
            error = "Долгота должна быть числом.\nПример: 37.6176";
            return false;
        }

        if (!TryParseCoordinate(LatTextBox.Text, out var lat))
        {
            error = "Широта должна быть числом.\nПример: 55.7558";
            return false;
        }

        if (lat is < -90 or > 90)
        {
            error = $"Широта {lat:0.####} вне диапазона.\nДопустимо: от −90 до 90.";
            return false;
        }

        if (lon is < -180 or > 180)
        {
            error = $"Долгота {lon:0.####} вне диапазона.\nДопустимо: от −180 до 180.";
            return false;
        }

        _draft.Latitude = Math.Round(lat, 6);
        _draft.Longitude = Math.Round(lon, 6);
        return true;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadCoordinates(out var error))
        {
            MessageBox.Show(this, error!, "Некорректные координаты",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var current = _settingsService.Current;
        current.Latitude = _draft.Latitude;
        current.Longitude = _draft.Longitude;
        current.Zoom = _draft.Zoom;
        current.CityName = "";
        current.ShowLabels = _draft.ShowLabels;
        current.ShowTrails = _draft.ShowTrails;

        // Интервал и стиль карты в UI не выставляются — оставляем как есть
        _settingsService.Save();

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadCoordinates(out var error))
        {
            MessageBox.Show(this, error!, "Некорректные координаты",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Проверка идёт с текущими (ещё не сохранёнными) значениями
        var current = _settingsService.Current;
        current.Latitude = _draft.Latitude;
        current.Longitude = _draft.Longitude;
        current.Zoom = _draft.Zoom;
        current.ShowLabels = _draft.ShowLabels;
        current.ShowTrails = _draft.ShowTrails;

        var preview = new ScreensaverWindow(
            _settingsService,
            new OpenSkyService(_settingsService),
            ScreensaverCommandLine.ForTestPreview())
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        preview.Show();
    }
}