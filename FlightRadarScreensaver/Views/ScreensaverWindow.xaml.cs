using FlightRadarScreensaver.Models;
using FlightRadarScreensaver.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace FlightRadarScreensaver.Views;

public partial class ScreensaverWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly OpenSkyService _openSkyService;
    private readonly ScreensaverCommandLine _cmdLine;

    private readonly DispatcherTimer _updateTimer;
    private readonly DispatcherTimer _hideInfoTimer;
    private readonly DispatcherTimer _loadTimeoutTimer;

    // Опрос позиции курсора: единственный способ поймать движение мыши
    // поверх WebView2, т.к. это отдельное HWND и оно съедает мышиные события WPF.
    private readonly DispatcherTimer _cursorTimer;
    private bool _cursorPrimed;
    private int _cursorX, _cursorY;

    private CancellationTokenSource? _flightCts;

    private bool _closing;

    private bool IsRealPreview =>
        _cmdLine.CurrentMode == ScreensaverCommandLine.Mode.Preview
        && _cmdLine.PreviewWindowHandle != IntPtr.Zero
        && !_cmdLine.IsTestPreview;

    public ScreensaverWindow(SettingsService settingsService, OpenSkyService openSkyService,
                             ScreensaverCommandLine cmdLine)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _openSkyService = openSkyService;
        _cmdLine = cmdLine;

        var interval = TimeSpan.FromSeconds(_settingsService.Current.UpdateIntervalSeconds);
        _updateTimer = new DispatcherTimer { Interval = interval };
        _updateTimer.Tick += async (_, _) => await UpdateFlightsAsync();

        _hideInfoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _hideInfoTimer.Tick += (_, _) =>
        {
            InfoPanel.Visibility = Visibility.Collapsed;
            _hideInfoTimer.Stop();
        };

        // Если карта не сообщила о готовности — снимаем оверлей, чтобы не остаться чёрным.
        _loadTimeoutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _loadTimeoutTimer.Tick += (_, _) => RevealMap("таймаут загрузки карты");

        _cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _cursorTimer.Tick += (_, _) => CheckCursorMovement();

        var appIcon = App.LoadAppIcon();
        if (appIcon != null) Icon = appIcon;

        // Ввод подключаем в коде с handledEventsToo: WebView2 — HWND-элемент,
        // он помечает события мыши/клавиатуры как обработанные, поэтому
        // обычные подписки из XAML их не видят.
        AddHandler(System.Windows.Input.Keyboard.KeyDownEvent,
            new System.Windows.Input.KeyEventHandler(Window_KeyDown), handledEventsToo: true);
        AddHandler(System.Windows.Input.Mouse.MouseMoveEvent,
            new System.Windows.Input.MouseEventHandler(Window_MouseMove), handledEventsToo: true);
        AddHandler(System.Windows.Input.Mouse.PreviewMouseDownEvent,
            new System.Windows.Input.MouseButtonEventHandler(Window_MouseDown), handledEventsToo: true);

        ConfigureWindowMode();
        Loaded += async (_, _) => await OnLoadedAsync();
        Closing += (_, _) => _closing = true;
        Closed += OnWindowClosed;
    }

    private void ConfigureWindowMode()
    {
        if (IsRealPreview)
        {
            // Встраиваемся в окно предпросмотра «Параметров экранной заставки».
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.NoResize;
            Topmost = false;
            Cursor = Cursors.Arrow;
            InfoPanel.Visibility = Visibility.Collapsed;
            _hideInfoTimer.Stop();

            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var parent = _cmdLine.PreviewWindowHandle;
            SetParent(helper.Handle, parent);
            SetWindowPos(helper.Handle, IntPtr.Zero, 0, 0, 0, 0,
                SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOZORDER |
                SetWindowPosFlags.SWP_NOACTIVATE | SetWindowPosFlags.SWP_SHOWWINDOW);
            return;
        }

        if (_cmdLine.IsTestPreview)
        {
            // Кнопка «Проверить»: обычное окно поверх настроек, управляется мышью.
            Title = "Предпросмотр — FlightRadar Screensaver (Esc — закрыть)";
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Normal;
            Width = 1100;
            Height = 680;
            ResizeMode = ResizeMode.CanResize;
            Topmost = false;
            Cursor = Cursors.Arrow;
            ShowInTaskbar = false;
            _hideInfoTimer.Stop();
            return;
        }

        // Обычный полноэкранный режим заставки.
        WindowState = WindowState.Maximized;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;

        // Выход по движению мыши — основной сценарий для заставки
        _cursorTimer.Start();
    }

    /// <summary>
    /// Сверяет текущую позицию курсора с предыдущей. Работает независимо от
    /// фокуса окна и от того, перехватил ли WebView2 мышиные события WPF.
    /// </summary>
    private void CheckCursorMovement()
    {
        if (_closing || !GetCursorPos(out var p)) return;

        if (!_cursorPrimed)
        {
            _cursorPrimed = true;
            _cursorX = p.X;
            _cursorY = p.Y;
            return;
        }

        int dx = Math.Abs(p.X - _cursorX);
        int dy = Math.Abs(p.Y - _cursorY);

        if (dx > 2 || dy > 2)
        {
            _cursorX = p.X;
            _cursorY = p.Y;
            ExitScreensaver($"mouse moved ({dx},{dy})");
        }
    }

    private async Task OnLoadedAsync()
    {
        if (_closing) return;

        if (_cmdLine.City != null || _cmdLine.Latitude.HasValue
            || _cmdLine.Longitude.HasValue || _cmdLine.Zoom.HasValue)
        {
            _settingsService.UpdateFromCommandLine(_cmdLine.City, _cmdLine.Latitude,
                _cmdLine.Longitude, _cmdLine.Zoom);
        }

        var settings = _settingsService.Current;
        _updateTimer.Interval = TimeSpan.FromSeconds(settings.UpdateIntervalSeconds);

        var webProfile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlightRadarScreensaver", "WebView2");

        try
        {
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: webProfile);

            // E_ABORT возникает, когда профилем WebView2 ещё владеет другой
            // экземпляр (например, предыдущий процесс не успел завершиться).
            // Без повторов заставка остаётся без карты.
            Exception? lastError = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await WebView.EnsureCoreWebView2Async(env);
                    lastError = null;
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    App.Log($"EnsureCoreWebView2Async attempt {attempt} failed: {ex.GetBaseException().Message}");
                    if (_closing) return;
                    await Task.Delay(1200 * attempt);
                }
            }

            if (lastError != null) throw lastError;

            var core = WebView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;

            // Политика тайловых провайдеров требует, чтобы приложение себя идентифицировало.
            core.Settings.UserAgent =
                "FlightRadarScreensaver/1.0 (Windows screensaver; OpenSky Network client)";
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.WebMessageReceived += OnWebMessageReceived;

            core.NavigationCompleted += (_, e) =>
            {
                if (!e.IsSuccess)
                {
                    App.Log($"NavigationCompleted FAILED: {e.WebErrorStatus}");
                    return;
                }

                App.Log("map.html loaded -> sending init to page");
                PostInitToPage();
                _ = LogPageDiagnosticsAsync();
            };

            var mapFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "map.html");
            if (File.Exists(mapFile))
            {
                core.Navigate(new Uri(mapFile).AbsoluteUri);
                _loadTimeoutTimer.Start();
            }
            else
            {
                App.Log("WebAssets/map.html NOT FOUND at " + mapFile);
                LoadingText.Text = "Файл карты не найден:\n" + mapFile;
                RevealMap("map.html отсутствует");
            }
        }
        catch (Exception ex)
        {
            App.Log("WebView2 init FAILED: " + ex);
            LoadingText.Text = "Не удалось запустить WebView2.\n" + ex.GetBaseException().Message;
            RevealMap("WebView2 недоступен");
        }
    }

    /// <summary>
    /// Отправляет странице стартовые параметры карты. Без этого map.html
    /// не построит карту и не пришлёт mapReady.
    /// </summary>
    private void PostInitToPage()
    {
        if (_closing) return;

        var s = _settingsService.Current;
        var payload = new
        {
            type = "init",
            lat = s.Latitude,
            lon = s.Longitude,
            zoom = s.Zoom,
            mapStyle = s.MapStyle,
            showLabels = s.ShowLabels,
            showTrails = s.ShowTrails
        };

        try
        {
            WebView.CoreWebView2?.PostWebMessageAsJson(JsonConvert.SerializeObject(payload));
        }
        catch (Exception ex)
        {
            App.Log("PostInitToPage failed: " + ex.Message);
        }
    }

    /// <summary>Разовая диагностика окружения страницы — пишется в crash-лог.</summary>
private async Task LogPageDiagnosticsAsync()
    {
        try
        {
            const string js = "JSON.stringify({"
                + "hasWebview: !!(window.chrome && window.chrome.webview),"
                + "leaflet: typeof L,"
                + "ready: document.readyState,"
                + "hasMapVar: (typeof map !== 'undefined' && map !== null),"
                + "scripts: document.scripts.length"
                + "})";

            var result = await WebView.CoreWebView2!.ExecuteScriptAsync(js);
            App.Log("page diagnostics: " + result);
        }
        catch (Exception ex)
        {
            App.Log("page diagnostics failed: " + ex.Message);
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            // Страница может прислать как объект, так и JSON-строку внутри —
            // в зависимости от способа вызова postMessage. Разбираем оба случая.
            var token = JToken.Parse(e.WebMessageAsJson);
            if (token.Type == JTokenType.String)
                token = JToken.Parse(token.Value<string>() ?? "{}");

            if (token is not JObject obj) return;

            var type = obj["type"]?.Value<string>();

            if (type == "error")
                App.Log("page JS error: " + obj["message"]?.Value<string>());
            else if (type == "tilesOk")
                App.Log("map tiles loading OK");
            else if (type == "diag")
                App.Log("canvas diag: " + obj["info"]?.Value<string>());
            else if (type == "mapReady")
            {
                App.Log("mapReady received from page");
                Dispatcher.Invoke(() => RevealMap("mapReady"));
            }
        }
        catch (Exception ex)
        {
            App.Log("WebMessage parse error: " + ex.Message);
        }
    }

    /// <summary>Снимает экран загрузки и стартует опрос данных.</summary>
    private void RevealMap(string reason)
    {
        if (_closing) return;
        _loadTimeoutTimer.Stop();

        LoadingOverlay.Visibility = Visibility.Collapsed;

        if (IsRealPreview) return; // в превью панель не нужна

        if (!_cmdLine.IsTestPreview)
            ShowInfoPanel();
        else
            InfoPanel.Visibility = Visibility.Collapsed;

        if (!_updateTimer.IsEnabled)
        {
            _updateTimer.Start();
            _ = UpdateFlightsAsync();
        }

        App.Log($"Map revealed ({reason}); flight polling started");
    }

    private void ShowInfoPanel()
    {
        var s = _settingsService.Current;
        InfoCity.Text = string.IsNullOrWhiteSpace(s.CityName) ? "Произвольные координаты" : s.CityName;
        InfoCoords.Text = $"{s.Latitude:F4}°, {s.Longitude:F4}°  ·  Zoom {s.Zoom}";
        InfoCount.Text = "Загрузка данных…";
        InfoUpdated.Text = DateTime.Now.ToString("HH:mm:ss");
        InfoPanel.Visibility = Visibility.Visible;
        _hideInfoTimer.Stop();
        _hideInfoTimer.Start();
    }

    private async Task UpdateFlightsAsync()
    {
        if (_closing) return;

        var s = _settingsService.Current;
        _flightCts?.Cancel();
        _flightCts = new CancellationTokenSource();
        var token = _flightCts.Token;

        try
        {
            var flights = await _openSkyService.GetFlightsAsync(
                s.Latitude, s.Longitude, s.Zoom, token);

            if (_closing || token.IsCancellationRequested) return;

            App.Log($"OpenSky: {flights.Count} aircraft in view (zoom {s.Zoom})");

            var payload = new
            {
                type = "updateFlights",
                flights = flights.Select(f => new
                {
                    icao24 = f.Icao24,
                    callsign = f.DisplayCallsign,
                    lat = f.Latitude,
                    lon = f.Longitude,
                    altitude = f.BaroAltitude,
                    velocity = f.Velocity,
                    track = f.TrueTrack,
                    verticalRate = f.VerticalRate,
                    onGround = f.OnGround,
                    originCountry = f.OriginCountry
                }).ToList()
            };

            Dispatcher.Invoke(() =>
            {
                if (_closing) return;
                try
                {
                    WebView.CoreWebView2?.PostWebMessageAsJson(JsonConvert.SerializeObject(payload));
                }
                catch (Exception ex)
                {
                    App.Log("PostWebMessageAsJson FAILED: " + ex.Message);
                }

                if (!_cmdLine.IsTestPreview)
                {
                    InfoCount.Text = $"Самолётов в зоне: {flights.Count}";
                    InfoUpdated.Text = $"Обновлено: {DateTime.Now:HH:mm:ss}";
                }
            });
        }
        catch (OperationCanceledException)
        {
            App.Log("Flight update cancelled");
        }
        catch (Exception ex)
        {
            App.Log("Flight update FAILED: " + ex.Message);
            if (!_cmdLine.IsTestPreview)
            {
                Dispatcher.Invoke(() =>
                {
                    InfoCount.Text = "Ошибка получения данных";
                    InfoUpdated.Text = DateTime.Now.ToString("HH:mm:ss");
                });
            }
        }
    }

    // ── Выход из заставки ─────────────────────────────────────────────
    /// <summary>
    /// Полное завершение приложения. Раньше здесь вызывался только Close():
    /// окно закрывалось, но процесс с живым WebView2 оставался на экране
    /// безоконным «осколком» вместо выхода.
    /// </summary>
    private void ExitScreensaver(string reason)
    {
        if (_closing) return;

        App.Log("EXIT: " + reason);
        _closing = true;

        _cursorTimer.Stop();
        _updateTimer.Stop();
        _hideInfoTimer.Stop();
        _loadTimeoutTimer.Stop();

        try { _flightCts?.Cancel(); } catch { }
        try { _flightCts?.Dispose(); } catch { }

        // Освобождаем HWND WebView2 — иначе он удерживает процесс
        try { WebView.Dispose(); } catch { }

        // Сторож: WebView2 держит дочерние процессы браузера, и Shutdown()
        // не всегда завершает приложение. Через пару секунд выходим жёстко.
        var watchdog = new Thread(() =>
        {
            Thread.Sleep(2500);
            Environment.Exit(0);
        })
        { IsBackground = true, Name = "ExitWatchdog" };
        watchdog.Start();

        try
        {
            Application.Current?.Shutdown();
        }
        catch (Exception ex)
        {
            App.Log("Shutdown failed: " + ex.Message);
            Environment.Exit(0);
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_closing) return;

        // В окне предпросмотра ввод игнорируем: этот режим запускает Windows
        // из «Параметров экранной заставки». Иначе Esc закрывает наш процесс,
        // а диалог тут же запускает его заново с /p — выглядит как «не выходит».
        if (IsRealPreview) return;

        if (_cmdLine.IsTestPreview)
        {
            if (e.Key == Key.Escape)
                ExitScreensaver("Esc в окне предпросмотра");
            return;
        }

        var isModifier = e.Key is Key.LeftShift or Key.RightShift
            or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin or Key.System;

        if (!isModifier)
            ExitScreensaver($"key {e.Key}");
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        // Основной способ отловить мышь — CheckCursorMovement().
        // Это событие срабатывает не всегда, т.к. ввод перехватывает WebView2.
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_cmdLine.IsTestPreview || IsRealPreview) return;

        ExitScreensaver($"mouse down ({e.ChangedButton})");
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        App.Log("Window closed");
        _cursorTimer.Stop();
        _updateTimer.Stop();
        _hideInfoTimer.Stop();
        _loadTimeoutTimer.Stop();

        if (Application.Current != null)
            Application.Current.Shutdown();
    }

    // ── WinAPI для встраивания в окно предпросмотра ──────────────────
    [Flags]
    private enum SetWindowPosFlags : uint
    {
        SWP_NOSIZE = 0x0001,
        SWP_NOMOVE = 0x0002,
        SWP_NOZORDER = 0x0004,
        SWP_NOACTIVATE = 0x0010,
        SWP_SHOWWINDOW = 0x0040,
        SWP_FRAMECHANGED = 0x0020
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, SetWindowPosFlags flags);
}