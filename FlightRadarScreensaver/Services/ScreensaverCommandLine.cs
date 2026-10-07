namespace FlightRadarScreensaver.Services;

/// <summary>
/// Разбирает аргументы командной строки по протоколу скринсейверов Windows.
/// /s — запуск, /p [hwnd] — предпросмотр, /c [hwnd] — настройки, /a [hwnd] — смена пароля.
/// Дополнительно поддерживаются /city, /lat, /lon, /zoom для переопределения настроек.
/// </summary>
public class ScreensaverCommandLine
{
    public enum Mode
    {
        Normal,
        Preview,
        Configure,
        Password
    }

    public Mode CurrentMode { get; private set; }
    public IntPtr PreviewWindowHandle { get; private set; }
    public bool IsTestPreview { get; private set; }

    public string? City { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public int? Zoom { get; private set; }

    /// <summary>Используется только фабрикой <see cref="ForTestPreview"/>.</summary>
    private ScreensaverCommandLine() => CurrentMode = Mode.Normal;

    public ScreensaverCommandLine(string[] args)
    {
        if (args.Length == 0)
        {
            // Без аргументов — обычный запуск заставки (двойной клик по .scr).
            CurrentMode = Mode.Normal;
            return;
        }

        var first = args[0].Trim().Trim('/').Trim('-').ToLowerInvariant();

        // Windows передаёт превью как /p <hwnd>
        if (first.StartsWith("p"))
        {
            CurrentMode = Mode.Preview;
            PreviewWindowHandle = ParseHandle(args);
        }
        else if (first.StartsWith("c"))
        {
            CurrentMode = Mode.Configure;
            PreviewWindowHandle = ParseHandle(args);
        }
        else if (first.StartsWith("a"))
        {
            CurrentMode = Mode.Password;
            PreviewWindowHandle = ParseHandle(args);
        }
        else if (first.StartsWith("s"))
        {
            CurrentMode = Mode.Normal;
            ParseOverrides(args);
        }
        else
        {
            CurrentMode = Mode.Normal;
            ParseOverrides(args);
        }
    }

    /// <summary>
    /// Режим кнопки «Проверить» в настройках: окно поверх родителя,
    /// не разворачивается на весь экран и не закрывается от движения мыши.
    /// </summary>
    public static ScreensaverCommandLine ForTestPreview() => new()
    {
        CurrentMode = Mode.Normal,
        IsTestPreview = true
    };

    private static IntPtr ParseHandle(string[] args)
    {
        // hwnd может быть числом либо "число" — Windows иногда передаёт его как строку.
        for (int i = 1; i < args.Length; i++)
        {
            if (long.TryParse(args[i], out var handle))
                return new IntPtr(handle);
        }
        return IntPtr.Zero;
    }

    private void ParseOverrides(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].Trim().Trim('/').Trim('-').ToLowerInvariant())
            {
                case "city":
                    if (i + 1 < args.Length) City = args[++i];
                    break;
                case "lat":
                    if (i + 1 < args.Length && TryParseDouble(args[i + 1], out var lat)) Latitude = lat;
                    break;
                case "lon":
                    if (i + 1 < args.Length && TryParseDouble(args[i + 1], out var lon)) Longitude = lon;
                    break;
                case "zoom":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var zoom)) Zoom = zoom;
                    break;
            }
        }
    }

    private static bool TryParseDouble(string text, out double value)
    {
        // Принимаем и точку, и запятую — зависит от локали системы.
        return double.TryParse(text.Replace(',', '.'),
                   System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}