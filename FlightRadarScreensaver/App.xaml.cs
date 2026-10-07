using FlightRadarScreensaver.Models;
using FlightRadarScreensaver.Services;
using FlightRadarScreensaver.Views;
using System.IO;
using System.Windows;

namespace FlightRadarScreensaver;

public partial class App : Application
{
    private SettingsService? _settingsService;
    private OpenSkyService? _openSkyService;

    public static string CrashLogPath => Path.Combine(
        Path.GetTempPath(), "FlightRadarScreensaver-crash.log");

    private static readonly object LogLock = new();

    public static void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";

        // Запись идёт с UI- и WebView-потоков плюс из таймеров, поэтому
        // AppendAllText регулярно падал на "файл занят" и строки молча терялись.
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                lock (LogLock)
                {
                    using var stream = new FileStream(CrashLogPath, FileMode.Append,
                        FileAccess.Write, FileShare.ReadWrite);
                    using var writer = new StreamWriter(stream);
                    writer.Write(line);
                }
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(15);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Иконка приложения из Assets\app.ico рядом с exe.
    /// Через XAML задать нельзя: относительный URI там ищется от папки файла разметки.
    /// </summary>
    public static System.Windows.Media.ImageSource? LoadAppIcon()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (!File.Exists(path)) return null;

            var image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            Log("LoadAppIcon failed: " + ex.Message);
            return null;
        }
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        Log($"Startup. Args: [{string.Join(" ", e.Args)}] BaseDir={AppDomain.CurrentDomain.BaseDirectory}");

        DispatcherUnhandledException += (s, args) =>
        {
            Log("DispatcherUnhandledException: " + args.Exception);
            args.Handled = true;
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Log("UnhandledException: " + args.ExceptionObject);
        };

        try
        {
            _settingsService = new SettingsService();
            _openSkyService = new OpenSkyService(_settingsService);

            var cmdLine = new ScreensaverCommandLine(e.Args);
            Log($"Mode={cmdLine.CurrentMode} Hwnd={cmdLine.PreviewWindowHandle}");

            switch (cmdLine.CurrentMode)
            {
                case ScreensaverCommandLine.Mode.Normal:
                    RunScreensaver(cmdLine);
                    break;
                case ScreensaverCommandLine.Mode.Preview:
                    RunPreview(cmdLine);
                    break;
                case ScreensaverCommandLine.Mode.Configure:
                    RunConfigure(cmdLine);
                    break;
                case ScreensaverCommandLine.Mode.Password:
                    Shutdown();
                    break;
            }
            Log("Startup complete, window shown.");
        }
        catch (Exception ex)
        {
            Log("Startup FATAL: " + ex);
            Shutdown(1);
        }
    }

    private void RunScreensaver(ScreensaverCommandLine cmdLine)
    {
        var window = new ScreensaverWindow(_settingsService!, _openSkyService!, cmdLine);
        window.Show();
    }

    private void RunPreview(ScreensaverCommandLine cmdLine)
    {
        var window = new ScreensaverWindow(_settingsService!, _openSkyService!, cmdLine);
        window.Show();
    }

    private void RunConfigure(ScreensaverCommandLine cmdLine)
    {
        var window = new SettingsWindow(_settingsService!);
        window.ShowDialog();
        Shutdown();
    }
}