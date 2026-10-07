<#
.SYNOPSIS
    Генерирует Assets\app.ico для FlightRadar Screensaver.
.DESCRIPTION
    Рисует иконку программно: скруглённый синий квадрат с белым самолётом
    (силуэт борта, вид сверху, носом вправо). Никаких внешних зависимостей.
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\Generate-Icon.ps1
#>
[CmdletBinding()]
param(
    [string]$OutputPath,
    [int[]]$Sizes = @(32, 48, 64, 128, 256)
)

$ErrorActionPreference = 'Stop'

# $PSScriptRoot недоступен в блоке param (PowerShell 5.1), поэтому путь считаем в теле
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $projectRoot = Split-Path $PSScriptRoot -Parent
    $OutputPath = Join-Path $projectRoot 'Assets\app.ico'
}

function Test-Poly {
    param([double]$Px, [double]$Py, [object[]]$Poly)
    $inside = $false
    $n = $Poly.Count
    $j = $n - 1
    for ($i = 0; $i -lt $n; $i++) {
        $xi = $Poly[$i][0]; $yi = $Poly[$i][1]
        $xj = $Poly[$j][0]; $yj = $Poly[$j][1]
        if ((($yi -gt $Py) -ne ($yj -gt $Py)) -and
            ($Px -lt (($xj - $xi) * ($Py - $yi) / ($yj - $yi) + $xi))) {
            $inside = -not $inside
        }
        $j = $i
    }
    return $inside
}

function Test-RoundRect {
    param([double]$Px, [double]$Py)
    $q = 0.20
    $cx = [Math]::Min([Math]::Max($Px, $q), 1 - $q)
    $cy = [Math]::Min([Math]::Max($Py, $q), 1 - $q)
    $dx = $Px - $cx; $dy = $Py - $cy
    return (($dx * $dx) + ($dy * $dy)) -le ($q * $q)
}

# Силуэт самолёта (нормированные координаты, Y вниз, нос вправо)
$Shapes = @(
    # Фюзеляж
    @((0.14, 0.50), (0.34, 0.452), (0.72, 0.452), (0.94, 0.50), (0.72, 0.548), (0.34, 0.548)),
    # Крыло (верхнее)
    @((0.42, 0.465), (0.505, 0.135), (0.585, 0.135), (0.565, 0.465)),
    # Крыло (нижнее)
    @((0.42, 0.535), (0.565, 0.535), (0.585, 0.865), (0.505, 0.865)),
    # Стабилизатор (верхний)
    @((0.215, 0.478), (0.255, 0.295), (0.305, 0.295), (0.305, 0.478)),
    # Стабилизатор (нижний)
    @((0.215, 0.522), (0.305, 0.522), (0.305, 0.705), (0.255, 0.705))
)

function New-IconImage {
    param([int]$Size)

    $hdr = [System.Collections.Generic.List[byte]]::new()
    $biSize = 40
    $height = $Size
    $hdr.AddRange([BitConverter]::GetBytes([int]$biSize))
    $hdr.AddRange([BitConverter]::GetBytes([int]$Size))
    $hdr.AddRange([BitConverter]::GetBytes([int]($height * 2)))   # XOR + AND
    $hdr.AddRange([BitConverter]::GetBytes([UInt16]1))
    $hdr.AddRange([BitConverter]::GetBytes([UInt16]32))
    $hdr.AddRange([BitConverter]::GetBytes([int]0))                # BI_RGB
    $hdr.AddRange([BitConverter]::GetBytes([int]($Size * $Size * 4)))
    $hdr.AddRange([BitConverter]::GetBytes([int]0))
    $hdr.AddRange([BitConverter]::GetBytes([int]0))
    $hdr.AddRange([BitConverter]::GetBytes([int]0))
    $hdr.AddRange([BitConverter]::GetBytes([int]0))

    $pixels = [System.Collections.Generic.List[byte]]::new()

    # DIB хранит строки снизу вверх
    for ($y = $height - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $Size; $x++) {
            $nx = ($x + 0.5) / $Size
            $ny = ($y + 0.5) / $Size

            $b = 0; $g = 0; $r = 0; $a = 0

            if (Test-RoundRect -Px $nx -Py $ny) {
                # Диагональный градиент #0A84FF -> #0058C7
                $t = ($nx + $ny) / 2
                $r = [int](0x0A + (0x00 - 0x0A) * $t)
                $g = [int](0x84 + (0x58 - 0x84) * $t)
                $b = [int](0xFF + (0xC7 - 0xFF) * $t)
                $a = 255

                foreach ($s in $Shapes) {
                    if (Test-Poly -Px $nx -Py $ny -Poly $s) {
                        $r = 255; $g = 255; $b = 255
                        break
                    }
                }
            }

            $pixels.Add([byte]$b); $pixels.Add([byte]$g)
            $pixels.Add([byte]$r); $pixels.Add([byte]$a)
        }
    }

    # AND-маска: 1 бит на пиксель, строки выровнены по 4 байта
    $maskStride = [int](([Math]::Floor(($Size + 31) / 32)) * 4)
    $mask = [System.Collections.Generic.List[byte]]::new()
    for ($y = 0; $y -lt $height; $y++) {
        $row = [byte[]]::new($maskStride)
        for ($x = 0; $x -lt $Size; $x++) {
            $nx = ($x + 0.5) / $Size
            $ny = ($y + 0.5) / $Size
            if (-not (Test-RoundRect -Px $nx -Py $ny)) {
                $byteIdx = [int][Math]::Floor($x / 8)
                $bit = 7 - ($x % 8)
                $row[$byteIdx] = $row[$byteIdx] -bor [byte](1 -shl $bit)
            }
        }
        $mask.AddRange($row)
    }

    $out = [System.Collections.Generic.List[byte]]::new()
    $out.AddRange($hdr)
    $out.AddRange($pixels)
    $out.AddRange($mask)
    return $out.ToArray()
}

# ── Собираем ICO-контейнер ─────────────────────────────────────────
$images = @()
foreach ($s in $Sizes) {
    Write-Host "  Рисую ${s}x${s}..."
    $imgData = New-IconImage -Size $s
    $images += , [pscustomobject]@{ Size = $s; Data = [byte[]]$imgData }
}

$dir = [System.Collections.Generic.List[byte]]::new()
$dir.AddRange([BitConverter]::GetBytes([UInt16]0))            # reserved
$dir.AddRange([BitConverter]::GetBytes([UInt16]1))            # type = icon
$dir.AddRange([BitConverter]::GetBytes([UInt16]$images.Count))

$offset = 6 + (16 * $images.Count)
$entries = [System.Collections.Generic.List[byte]]::new()

foreach ($img in $images) {
    $dim = if ($img.Size -ge 256) { [byte]0 } else { [byte]$img.Size }
    $entries.Add($dim)
    $entries.Add($dim)
    $entries.Add([byte]0)                                     # палитра не используется
    $entries.Add([byte]0)
    $entries.AddRange([BitConverter]::GetBytes([UInt16]1))     # planes
    $entries.AddRange([BitConverter]::GetBytes([UInt16]32))    # bpp
    $entries.AddRange([BitConverter]::GetBytes([int]$img.Data.Length))
    $entries.AddRange([BitConverter]::GetBytes([int]$offset))
    $offset += $img.Data.Length
}

$all = [System.Collections.Generic.List[byte]]::new()
$all.AddRange($dir)
$all.AddRange($entries)
foreach ($img in $images) { $all.AddRange([byte[]]$img.Data) }

$outDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
[System.IO.File]::WriteAllBytes($OutputPath, $all.ToArray())

Write-Host ""
Write-Host "OK: $OutputPath ($($all.Count) bytes, $($images.Count) размеров)" -ForegroundColor Green