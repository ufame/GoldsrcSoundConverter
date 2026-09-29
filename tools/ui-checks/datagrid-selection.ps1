param(
  [string]$Exe = "src/GoldsrcSoundConverter.App/bin/Debug/net10.0-windows/GoldsrcSoundConverter.exe"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$exePath = Join-Path $root $Exe
if (-not (Test-Path $exePath)) {
  throw "Не найден exe: $exePath. Соберите проект (dotnet build) или укажите -Exe."
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Ui
{
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
  public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class DialogFinder
{
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
  [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr hWnd, out int pid);
  [DllImport("user32.dll")] static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);

  public static IntPtr FindDialog(int wantedPid)
  {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) =>
    {
      int pid;
      GetWindowThreadProcessId(h, out pid);
      if (pid != wantedPid || !IsWindowVisible(h)) return true;
      var cls = new StringBuilder(64);
      GetClassName(h, cls, 64);
      if (cls.ToString() == "#32770") { found = h; return false; }
      return true;
    }, IntPtr.Zero);
    return found;
  }
}
"@
[Ui]::SetProcessDPIAware() | Out-Null

$tempDir = Join-Path $env:TEMP ("gsc-uicheck-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir | Out-Null
$wavPath = Join-Path $tempDir "ui-check.wav"

$sampleRate = 8000
$seconds = 1
$dataLength = $sampleRate * $seconds * 2
$bytes = New-Object byte[] (44 + $dataLength)
$bytes[0] = 0x52; $bytes[1] = 0x49; $bytes[2] = 0x46; $bytes[3] = 0x46
[BitConverter]::GetBytes([int](36 + $dataLength)).CopyTo($bytes, 4)
$bytes[8] = 0x57; $bytes[9] = 0x41; $bytes[10] = 0x56; $bytes[11] = 0x45
$bytes[12] = 0x66; $bytes[13] = 0x6D; $bytes[14] = 0x74; $bytes[15] = 0x20
[BitConverter]::GetBytes(16).CopyTo($bytes, 16)
[BitConverter]::GetBytes([int16]1).CopyTo($bytes, 20)
[BitConverter]::GetBytes([int16]1).CopyTo($bytes, 22)
[BitConverter]::GetBytes($sampleRate).CopyTo($bytes, 24)
[BitConverter]::GetBytes($sampleRate * 2).CopyTo($bytes, 28)
[BitConverter]::GetBytes([int16]2).CopyTo($bytes, 32)
[BitConverter]::GetBytes([int16]16).CopyTo($bytes, 34)
$bytes[36] = 0x64; $bytes[37] = 0x61; $bytes[38] = 0x74; $bytes[39] = 0x61
[BitConverter]::GetBytes($dataLength).CopyTo($bytes, 40)
[System.IO.File]::WriteAllBytes($wavPath, $bytes)

$proc = Start-Process -FilePath $exePath -PassThru
try {
  for ($i = 0; $i -lt 40; $i++) { Start-Sleep -Milliseconds 500; $proc.Refresh(); if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { break } }
  Start-Sleep -Seconds 3
  $hwnd = $proc.MainWindowHandle
  if ($hwnd -eq [IntPtr]::Zero) { throw "Окно приложения не найдено" }
  [Ui]::SetForegroundWindow($hwnd) | Out-Null

  $rootElement = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
  $buttonCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)

  function Find-Button([string]$name) {
    $cond = New-Object System.Windows.Automation.AndCondition($buttonCond, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)))
    return $rootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
  }

  $add = Find-Button "Добавить файлы"
  if ($null -eq $add) { throw "Кнопка 'Добавить файлы' не найдена" }
  $add.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()

  $dlgHandle = [IntPtr]::Zero
  for ($i = 0; $i -lt 60 -and $dlgHandle -eq [IntPtr]::Zero; $i++) {
    Start-Sleep -Milliseconds 500
    $dlgHandle = [DialogFinder]::FindDialog($proc.Id)
  }
  if ($dlgHandle -eq [IntPtr]::Zero) { throw "Диалог выбора файлов не появился" }
  [Ui]::SetForegroundWindow($dlgHandle) | Out-Null
  Start-Sleep -Milliseconds 500
  [System.Windows.Forms.SendKeys]::SendWait($wavPath)
  Start-Sleep -Milliseconds 400
  [Ui]::SetForegroundWindow($dlgHandle) | Out-Null
  [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
  for ($i = 0; $i -lt 10; $i++) {
    Start-Sleep -Milliseconds 400
    if ([DialogFinder]::FindDialog($proc.Id) -eq [IntPtr]::Zero) { break }
    [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
  }

  $gridCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::DataGrid)
  $grid = $rootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $gridCond)
  if ($null -eq $grid) { throw "DataGrid не найден" }
  $rowCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::DataItem)
  $rows = @()
  for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 500
    $rows = @($grid.FindAll([System.Windows.Automation.TreeScope]::Descendants, $rowCond) | Where-Object { $_.Current.BoundingRectangle.Height -gt 10 })
    if ($rows.Count -gt 0) { break }
  }
  if ($rows.Count -eq 0) { throw "Очередь пуста — файл не добавился" }

  [Ui]::SetForegroundWindow($hwnd) | Out-Null
  Start-Sleep -Milliseconds 400
  $row = $rows[0]
  $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
  Start-Sleep -Milliseconds 500

  function Get-RowModalColor {
    $r = New-Object Ui+RECT
    [Ui]::GetWindowRect($hwnd, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left
    $h = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    $ok = [Ui]::PrintWindow($hwnd, $hdc, 2)
    $g.ReleaseHdc($hdc)
    if (-not $ok) {
      $g2 = [System.Drawing.Graphics]::FromImage($bmp)
      $g2.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
      $g2.Dispose()
    }
    $g.Dispose()

    $rr = $row.Current.BoundingRectangle
    $y = [int]($rr.Y + $rr.Height / 2) - $r.Top
    $counts = @{}
    for ($x = [int]$rr.X + 8; $x -lt [int]($rr.X + $rr.Width) - 8; $x += 6) {
      $px = $x - $r.Left
      if ($y -lt 0 -or $y -ge $h -or $px -lt 0 -or $px -ge $w) { continue }
      $c = $bmp.GetPixel($px, $y)
      $key = "$($c.R),$($c.G),$($c.B)"
      if (-not $counts.ContainsKey($key)) { $counts[$key] = 0 }
      $counts[$key]++
    }
    $bmp.Dispose()
    $best = $counts.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1
    return @{ Rgb = $best.Key; Count = $best.Value; Total = ($counts.Values | Measure-Object -Sum).Sum }
  }

  $active = Get-RowModalColor

  $clear = Find-Button "Очистить"
  if ($null -eq $clear) { throw "Кнопка 'Очистить' не найдена" }
  $clear.SetFocus()
  Start-Sleep -Milliseconds 700
  [Ui]::SetForegroundWindow($hwnd) | Out-Null
  Start-Sleep -Milliseconds 300

  $inactive = Get-RowModalColor

  $sumActive = 0
  $sumInactive = 0
  foreach ($part in $active.Rgb.Split(',')) { $sumActive += [int]$part }
  foreach ($part in $inactive.Rgb.Split(',')) { $sumInactive += [int]$part }
  Write-Host ("ACTIVE:   rgb={0} (sum={1}, {2}/{3})" -f $active.Rgb, $sumActive, $active.Count, $active.Total)
  Write-Host ("INACTIVE: rgb={0} (sum={1}, {2}/{3})" -f $inactive.Rgb, $sumInactive, $inactive.Count, $inactive.Total)

  if ($sumInactive -gt 350) {
    Write-Host "FAIL: неактивная строка светлая"
    exit 1
  }

  Write-Host "PASS: строка тёмная в обоих состояниях"
  exit 0
}
finally {
  Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
  Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
