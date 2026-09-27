param(
  [switch]$SkipTests,
  [switch]$NoZip,
  [string]$Version
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$solution = Join-Path $root "GoldsrcSoundConverter.slnx"
$project = Join-Path $root "src/GoldsrcSoundConverter.App/GoldsrcSoundConverter.App.csproj"
$dist = Join-Path $root "publish"
$output = Join-Path $dist "win-x64"

if (-not $Version)
{
  [xml]$csproj = Get-Content -LiteralPath $project
  $Version = [string](($csproj.Project.PropertyGroup | Where-Object { $_.Version }) | Select-Object -First 1).Version
}

if (-not $Version) { throw "Не удалось определить версию, укажите -Version x.y.z" }
Write-Host "Версия: $Version"

if (-not $SkipTests)
{
  Write-Host "`nТесты..."
  dotnet test $solution -c Release --nologo
  if ($LASTEXITCODE -ne 0) { throw "Тесты не прошли, публикация отменена" }
}

if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
New-Item -ItemType Directory -Path $output -Force | Out-Null

Write-Host "`nПубликация (self-contained, single-file)..."
dotnet publish $project -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:Version=$Version `
  -o $output
if ($LASTEXITCODE -ne 0) { throw "Публикация не удалась" }

$guide = @"
Goldsrc Sound Converter — как пользоваться
==========================================

1. Запустите GoldsrcSoundConverter.exe. Установка не требуется, .NET ставить
   не нужно. Если Windows покажет предупреждение SmartScreen — нажмите
   «Подробнее» и «Всё равно запустить».

2. При первой конвертации программе нужен интернет: она сама скачает FFmpeg
   (~80 МБ). Если интернета нет, укажите свой ffmpeg.exe в настройках.

3. Добавьте файлы или папки (кнопками или перетащите в окно).

4. Выберите формат и пресет:
     WAV — звуки ....... для папки sound\ в CS 1.6 (22050 Hz, 16-bit, mono)
     WAV — голос/UI .... 11025 Hz, для голоса и старых звуков Half-Life
     MP3 — музыка ...... для папки mp3\ (команда mp3 play в игре)

5. Укажите папку результатов и нажмите «Начать конвертацию».

Куда класть готовые файлы:
     WAV -> <папка игры>\cstrike\sound\...
     MP3 -> <папка игры>\cstrike\mp3\...

Программа сама приводит имена к нижнему регистру и ASCII (транслит кириллицы)
и сохраняет структуру папок.

Служебные данные (можно удалять, пересоздаются автоматически):
     настройки: %AppData%\GoldsrcSoundConverter
     FFmpeg:    %LocalAppData%\GoldsrcSoundConverter\ffmpeg
"@
$guidePath = Join-Path $output "Как пользоваться.txt"
Set-Content -LiteralPath $guidePath -Value $guide -Encoding utf8

Write-Host "`nСодержимое $output :"
Get-ChildItem -LiteralPath $output | Select-Object Name, Length | Format-Table -AutoSize

if (-not $NoZip)
{
  $zip = Join-Path $dist "GoldsrcSoundConverter-$Version-win-x64.zip"
  if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }

  Add-Type -AssemblyName System.IO.Compression.FileSystem
  [System.IO.Compression.ZipFile]::CreateFromDirectory(
    $output, $zip,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false,
    [System.Text.Encoding]::UTF8)

  Write-Host "Готово: $zip"
}
else
{
  Write-Host "Готово: $output"
}
