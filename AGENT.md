# AGENT.md

Файл для ИИ-агентов и автоматизации, работающих с этим репозиторием.
Человеческую инструкцию по разработке см. в [CONTRIBUTING.md](CONTRIBUTING.md).

## Что это

Нативный скринсейвер Windows (`.scr`) с живой картой самолётов.
Показывает реальные данные о полётах из **OpenSky Network** поверх карты
Leaflet, отрисованной в WebView2. Точка и масштаб карты задаёт пользователь.

НеFlightRadar24 и не их API: официальный API FlightRadar24 закрыт и платный.
Источник данных — открытый ADS-B OpenSky Network.

## Стек

| Слой | Технология |
|---|---|
| Язык / рантайм | C# / .NET 8 (`net8.0-windows`) |
| UI | WPF (код + XAML), без сторонних UI-фреймворков |
| Карта | Leaflet 1.9.4 в WebView2 (`Microsoft.Web.WebView2`) |
| JSON | Newtonsoft.Json |
| Тайлы | Esri ArcGIS REST (без ключей) |
| Сборка | `dotnet` CLI, без post-build скриптов |

Внешних зависимостей, кроме перечисленных пакетов, нет. Дизайн-система
(стили кнопок, полей, списков, слайдера) написана вручную в `App.xaml`.

## Команды

Рабочий каталог — корень репозитория.

```powershell
# Сборка (должна быть без предупреждений)
dotnet build FlightRadarScreensaver/FlightRadarScreensaver.csproj -c Release

# Готовый набор файлов для установки (12 файлов, нужен .NET 8 Runtime)
dotnet publish FlightRadarScreensaver/FlightRadarScreensaver.csproj -c Release -o publish
Copy-Item publish/FlightRadarScreensaver.exe publish/FlightRadarScreensaver.scr

# Без установленного .NET (~200 файлов)
dotnet publish FlightRadarScreensaver/FlightRadarScreensaver.csproj `
  -c Release -r win-x64 --self-contained true -o publish

# Запуск
.\publish\FlightRadarScreensaver.exe /c   # диалог настроек
.\publish\FlightRadarScreensaver.exe /s   # заставка
.\publish\FlightRadarScreensaver.scr /s /lat 55.7558 /lon 37.6176 /zoom 9

# Иконка приложения (генерируется программно, результат коммитится)
powershell -ExecutionPolicy Bypass -File FlightRadarScreensaver/tools/Generate-Icon.ps1
```

Сборка только на Windows. Тестового проекта нет — проверка ручная и
описана ниже.

## Архитектура

```
App.xaml.cs                          точка входа, разбор режима (/s /p /c /a),
                                     обработчики необработанных исключений, лог
Models/
  Settings.cs                        настройки + пресеты городов (только для /city)
  FlightData.cs                      разбор ответа OpenSky (17 полей на борт)
Services/
  ScreensaverCommandLine.cs          разбор аргументов и режим предпросмотра
  OpenSkyService.cs                  HTTP-клиент, bounding box, лимит бортов
  SettingsService.cs                 чтение/запись %LOCALAPPDATA%\...\settings.json
Views/
  ScreensaverWindow.xaml(.cs)        полноэкранная заставка
  SettingsWindow.xaml(.cs)           диалог настроек (координаты, зум, подписи)
WebAssets/map.html                   вся отрисовка: canvas, самолёты, подписи,
                                     клики, тайлы
Assets/app.ico                       иконка (генерируется tools/Generate-Icon.ps1)
tools/Generate-Icon.ps1              генератор иконки
```

### Поток данных

```
OpenSkyService.GetFlightsAsync        GET /api/states/all?lamin&lomin&lamax&lomax
        ↓  FlightData[] (<=1200 бортов, ближайших к центру карты)
C# формирует JSON                     PostWebMessageAsJson("init" | "updateFlights")
        ↓
map.html                              init   → строит карту, отвечает mapReady
        ↓                             updateFlights → рисует борта
WebMessageReceived                    mapReady | tilesOk | error | diag
```

Обратный канал нужен для снятия экрана загрузки и диагностики.

## Правила, нарушение которых ломает проект

1. **XAML: только 6-значные hex-цвета.** `#ccc` — это CSS, WPF такой формат
   не понимает: `XamlParseException`, окно не открывается. Нужно `#CCCCCC`.
2. **XAML: `Fill="None"` недопустим.** Только `Transparent`, `#RRGGBB`, `#AARRGGBB`.
3. **Иконку окна нельзя задать в XAML** через `Icon="Assets/app.ico"`:
   относительный URI ищется от папки разметки (`views/assets/app.ico`).
   Используется `App.LoadAppIcon()`.
4. **WebView2 — HWND-элемент**, он перехватывает мышь и клавиатуру.
   Маршрутизированные события WPF до окна не доходят, поэтому ввод
   подключён через `AddHandler(..., handledEventsToo: true)` и опрос позиции
   курсора `GetCursorPos`. Обычные подписки в XAML не работают.
5. **Формат сообщений C# ↔ страница.** `PostWebMessageAsJson` передаёт в JS
   объект, поэтому JS обязан слать объект, а не `JSON.stringify(...)`.
   Иначе C# получит строковый литерал и не найдёт поле `type`.
6. **Слушатель `message` в map.html регистрируется при загрузке страницы**,
   а не внутри функции инициализации. Иначе команда `init` не дойдёт,
   карта не построится, останется чёрный экран.
7. **Тайлы `tile.openstreetmap.org` использовать нельзя** — политика
   использования запрещает встраивание, сервер отвечает 403
   («Access blocked»). Тайлы CARTO требуют API-ключ и отдают заглушки
   «API KEY REQUIRED». Рабочий вариант — сервисы Esri без ключей.
8. **Фильтрация бортов — по расстоянию до центра карты** (`Limit()` в
   `OpenSkyService`). Сортировка по модулю координат выберет самолёты
   над Атлантикой вместо нужного региона.
9. **Отрисовка самолётов — один `<canvas>`**, а не DOM-маркеры. Тысяча
   `div` с SVG уронит FPS. Подписи защищены от перекрытия проверкой
   прямоугольников.

## Диагностика

Приложение пишет лог: `%TEMP%\FlightRadarScreensaver-crash.log`

```
[12:29:05] Mode=Normal Hwnd=0
[12:29:05] map.html loaded -> sending init to page
[12:29:05] mapReady received from page
[12:29:05] Map revealed (mapReady); flight polling started
[12:29:05] map tiles loading OK
[12:29:07] OpenSky request bbox=[51,58..53,04, 3,57..5,95] -> 7233 aircraft
[12:29:07] OpenSky: 1200 aircraft in view (zoom 9)
[12:29:07] canvas diag: {"opaquePixels":9906,"visibleAircraft":207,...}
```

При разборе проблем это первый источник информации.

Ключевые поля: `mode` — режим запуска; `mapReady` — страница построила карту;
`tilesOk` — тайлы пошли; `OpenSky: N` — сколько бортов отрисовано;
`EXIT: <причина>` — чем завершилась заставка; `canvas diag` — метрики холста
(`opaquePixels: 0` при `visibleAircraft > 0` означает сбой отрисовки).

Типичные сообщения и что они значат:

| Сообщение | Значение |
|---|---|
| `OpenSky request FAILED: ... 429` | исчерпан лимит ~400 запросов/сутки, подождать |
| `tiles failing for style '...'` | провайдер тайлов недоступен или заблокировал |
| `EnsureCoreWebView2Async attempt N failed` | профиль занят другим экземпляром, есть повторы |
| `page JS error: ...` | ошибка в map.html, смотрить консоль страницы |
| `canvas diag: opaquePixels: 0` | самолёты не рисуются при непустых данных |

## Ручная проверка

Автотестов нет. Проверять следует так (окно видно, можно снять скриншот):

1. `exe /c` — открывается диалог; ввести координаты, двигать слайдер зума,
   нажать «Сохранить» и «Проверить».
2. `exe /s /lat 52.31 /lon 4.76 /zoom 9` — карта и самолёты появляются,
   у бортов видны подписи.
3. Выход по Esc и по движению мыши — процесс завершается полностью,
   в логе `EXIT: <причина>`.
4. Клик по самолёту — открывается карточка с данными.
5. `install.bat` от администратора — файлы копируются в `%windir%\System32`,
   заставка появляется в `desk.cpl`.

## Ограничения данных

- **Маршрута «откуда-куда» в бесплатном API нет.** В `/api/states/all` его
  не существует, а `/api/routes` отдаёт `404` для анонимных запросов
  (только для партнёров). В подписи борта — номер рейса, страна
  регистрации и высота. Для настоящих маршрутов нужен платный API.
- **Видны не все борта** — только те, что принимают наземные приёмники;
  MLAT не отображается.
- **Лимит ~400 запросов в сутки** с одного IP. Интервал по умолчанию 30 с.
- Данные OpenSky — CC BY-NC-SA, **только некоммерческое использование**.

## CI/CD

`.github/workflows/build.yml` — на каждый push в `main` и в PR:
restore → build (Release) → publish → создание `.scr` → **проверка 10
обязательных файлов** (иначе exit 1) → zip → `upload-artifact`.

`.github/workflows/release.yml` — при пуше тега `v*`: self-contained сборка
`win-x64`, `.scr`, README, LICENSE, заметки из `CHANGELOG.md`, публикация
GitHub Release через `softprops/action-gh-release`.

Правила работы с `main`:

- Прямые пуши в `main` запрещены — защита ветки, только через Pull Request.
- Релиз создаётся **только после успешной сборки**: сначала зелёный статус
  на `main`, потом тег.
- Версии — по SemVer, записи — в `CHANGELOG.md` в формате Keep a Changelog.

## Проверка перед коммитом

```powershell
dotnet build FlightRadarScreensaver/FlightRadarScreensaver.csproj -c Release
```

Ожидается: `Сборка успешно завершена. Предупреждений: 0`.
Предупреждения `CA1416` про WebView2 безвредны (проект и так `net8.0-windows`),
но новых быть не должно.

Не коммитить: `bin/`, `obj/`, `publish/`, `artifacts/`, `*.scr`, `*.zip`.
Обязательные файлы сборки (`Assets/app.ico`, `WebAssets/*`, `install.bat`)
коммитятся и проверяются в CI.