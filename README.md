<div align="center">

# FlightRadar Screensaver

**Заставка Windows с живой картой самолётов**

Нативный `.scr` на .NET 8 + WPF: реальные полёты из **OpenSky Network**,
карта Leaflet в WebView2, настройка точки и масштаба.

[![Build](https://github.com/ozyab09/flight-screensaver/actions/workflows/build.yml/badge.svg)](https://github.com/ozyab09/flight-screensaver/actions/workflows/build.yml)
[![Release](https://github.com/ozyab09/flight-screensaver/actions/workflows/release.yml/badge.svg)](https://github.com/ozyab09/flight-screensaver/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)

</div>

---

## Возможности

| | |
|---|---|
| 🗺️ | Карта с тайлами Esri: улицы, спутник, тёмная тема |
| ✈️ | Полёты в реальном времени из OpenSky Network (открытый API, без ключей) |
| 🏷️ | Подпись каждого борта: номер рейса, страна регистрации, высота |
| 🖱️ | Клик по самолёту — высота, скорость, курс, вертикальная скорость |
| 🎚️ | Настройка точки карты вручную и масштаба (3–15) |
| ⏱️ | Автообновление каждые 30 секунд |
| 🎨 | Иконка приложения, аккуратный диалог настроек |
| 🛡️ | Выход по любой клавише или движению мыши |

---

## Установка

### Скачать готовое

1. Скачайте архив
   [`FlightRadarScreensaver-v1.0.0-win-x64.zip`](https://github.com/ozyab09/flight-screensaver/releases/latest)
   из раздела [Releases](https://github.com/ozyab09/flight-screensaver/releases)
2. Распакуйте в любую папку
3. Запустите **`install.bat`** от имени администратора
4. `Win+R` → `desk.cpl` → вкладка **Заставка** → **FlightRadarScreensaver**
5. **Параметры…** → введите координаты и масштаб → **Сохранить**
6. **Просмотр** или **ОК**

### Собрать из исходников

Нужны Windows 10/11 x64 и [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/ozyab09/flight-screensaver.git
cd FlightRadarScreensaver

# Готовый набор файлов для установки
dotnet publish FlightRadarScreensaver/FlightRadarScreensaver.csproj -c Release -o publish
Copy-Item publish/FlightRadarScreensaver.exe publish/FlightRadarScreensaver.scr
.\publish\install.bat          # от администратора
```

<details>
<summary>Сборка без установленного .NET (self-contained)</summary>

```powershell
dotnet publish FlightRadarScreensaver/FlightRadarScreensaver.csproj `
  -c Release -r win-x64 --self-contained true -o publish
```
Получится ~200 файлов, но .NET на целевой машине не нужен.
</details>

<details>
<summary>Ручная установка без скрипта</summary>

```powershell
# Для всех пользователей (нужны права администратора):
Copy-Item publish/FlightRadarScreensaver.scr $env:windir\System32\

# Только для текущего пользователя (админ не нужен):
Copy-Item publish/FlightRadarScreensaver.scr `
  "$env:LOCALAPPDATA\Microsoft\Windows\Themes\"
```
</details>

> **Важно:** `.scr` работает только рядом со своими DLL и папкой `WebAssets`.
> Копировать нужно **весь набор файлов**, а не один `.scr`.

---

## Настройка

| Параметр | Где задаётся | Значения |
|---|---|---|
| Долгота, широта | диалог «Параметры…», `appsettings.json`, `/lon` `/lat` | −180…180 / −90…90 |
| Масштаб (Zoom) | диалог, `appsettings.json`, `/zoom` | 3 — весь мир, 15 — район города |
| Подписи самолётов | диалог | вкл / выкл |
| Следы полётов | диалог | вкл / выкл |

Прямой запуск с параметрами:

```powershell
.\publish\FlightRadarScreensaver.scr /s /lat 55.7558 /lon 37.6176 /zoom 9
.\publish\FlightRadarScreensaver.exe /c      # диалог настроек
```

Режимы командной строки: `/s` — заставка, `/p <hwnd>` — предпросмотр,
`/c` — настройки, `/a` — пароль (не поддерживается).

Настройки сохраняются в `%LOCALAPPDATA%\FlightRadarScreensaver\settings.json`
и переживают обновление `.scr`.

---

## Что умеет заставка

- **Выход** по любой клавише, движению мыши или клику
- **Клик по самолёту** — карточка с высотой, скоростью, курсом и вертикальной скоростью
- **Следы полётов** — траектория последних позиций (по умолчанию выключено)
- **Подписи** не налезают друг на друга: перекрывающиеся подписи пропускаются

---

## Данные и ограничения

**Источник:** [OpenSky Network](https://opensky-network.org/apidoc/rest.html) —
открытый API ADS-B, бесплатно и без ключа.

Честно о том, чего в нём **нет**:

- **Маршрута «откуда-куда» нет.** В `/api/states/all` его не существует, а
  эндпоинт `/api/routes` отдаёт `404` для анонимных запросов (только для
  партнёров). Поэтому подпись содержит номер рейса, страну регистрации и
  высоту. Полноценные маршруты требуют платного API (FlightAware, AviationStack).
- **Не все борта видны.** Только те, что видят наземные приёмники; MLAT
  не отображается. Число самолётов зависит от плотности сети в регионе.
- **Лимит ~400 запросов в сутки** с одного IP. Интервал 30 секунд — с запасом.
- **Борт на земле** определяется по флагу `on_ground`.

---

## Требования

| | |
|---|---|
| ОС | Windows 10 1809+ / Windows 11, x64 |
| .NET 8 Desktop Runtime | входит в self-contained-сборку; на Windows 11 предустановлен |
| WebView2 Runtime | предустановлен в Windows 11; на Windows 10 подтягивается автоматически |
| Интернет | тайлы карты и API |

---

## Разработка

```powershell
# Сборка
dotnet build FlightRadarScreensaver/FlightRadarScreensaver.csproj -c Release

# Иконка приложения (генерируется программно)
powershell -ExecutionPolicy Bypass -File FlightRadarScreensaver/tools/Generate-Icon.ps1
```

### Диагностика

Приложение пишет подробный лог с причинами всех событий:

```
%TEMP%\FlightRadarScreensaver-crash.log
```

```text
[12:29:05] Mode=Normal Hwnd=0
[12:29:05] map.html loaded -> sending init to page
[12:29:05] mapReady received from page
[12:29:05] Map revealed (mapReady); flight polling started
[12:29:07] OpenSky request bbox=[51,58..53,04, 3,57..5,95] -> 7233 aircraft
[12:29:07] OpenSky: 1200 aircraft in view (zoom 9)
```

Подробности разработки и список подводных камней WPF/WebView2 —
в [CONTRIBUTING.md](CONTRIBUTING.md).

---

## Структура проекта

```
FlightRadarScreensaver/
├── Models/
│   ├── Settings.cs            настройки и пресеты городов
│   └── FlightData.cs          разбор ответа OpenSky
├── Services/
│   ├── OpenSkyService.cs      HTTP-клиент и расчёт области запроса
│   ├── SettingsService.cs     сохранение настроек в %LOCALAPPDATA%
│   └── ScreensaverCommandLine.cs  разбор /s /p /c /a
├── Views/
│   ├── ScreensaverWindow.*    полноэкранная заставка
│   └── SettingsWindow.*       диалог настроек
├── WebAssets/map.html         отрисовка самолётов на canvas
├── Assets/app.ico             иконка
└── tools/Generate-Icon.ps1    генератор иконки
```

Самолёты рисуются на одном `<canvas>`, а не тысячами DOM-элементов — это
держит стабильный FPS при любом количестве бортов в зоне.

---

## Благодарности

- [OpenSky Network](https://opensky-network.org/) — данные о полётах
- [Leaflet](https://leafletjs.com/) — карта (BSD-2-Clause)
- [Microsoft WebView2](https://learn.microsoft.com/microsoft-edge/webview2/) — встроенный браузер
- Тайлы: © Esri, HERE, Garmin, © OpenStreetMap contributors

---

## Лицензия

MIT — см. [LICENSE](LICENSE).

Данные OpenSky Network распространяются по **CC BY-NC-SA** и пригодны только
для некоммерческого использования. Это ограничение распространяется и на
сборки на основе проекта.