<#
  Builds the PedalKeys helper and refreshes the zip served by the website.

  Needs nothing but Windows: it uses the C# compiler that ships with the built-in
  .NET Framework 4.x (no .NET SDK, no Visual Studio, no admin rights).

  Output:
    helper/dist/PedalKeys/            staged files (exe, setup.bat, ...)
    public/downloads/PedalKeys-Windows.zip
#>
param(
  [switch]$NoZip
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $PSScriptRoot 'src'
$pkg = Join-Path $PSScriptRoot 'package'
$stage = Join-Path $PSScriptRoot 'dist\PedalKeys'
$zip = Join-Path $root 'public\downloads\PedalKeys-Windows.zip'

# Prefer 64-bit csc; AnyCPU output then runs 64-bit on 64-bit Windows.
$csc = @(
  "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
  "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw 'csc.exe (.NET Framework 4.x) not found.' }

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

$fx = Split-Path $csc
$wpf = Join-Path $fx 'WPF'

Write-Host "Compiling with $csc"
& $csc /nologo /target:winexe /optimize+ /platform:anycpu `
  /out:"$stage\PedalKeys.exe" `
  /win32manifest:"$src\app.manifest" `
  /reference:System.dll /reference:System.Core.dll `
  /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
  /reference:"$wpf\WindowsBase.dll" `
  /reference:"$wpf\UIAutomationTypes.dll" `
  /reference:"$wpf\UIAutomationClient.dll" `
  (Get-ChildItem "$src\*.cs" | ForEach-Object { $_.FullName })
if ($LASTEXITCODE -ne 0) { throw "Compile failed ($LASTEXITCODE)" }

Copy-Item "$pkg\*" $stage

# .bat files must use CRLF line endings.
Get-ChildItem $stage -Include *.bat, *.txt -Recurse | ForEach-Object {
  $text = [IO.File]::ReadAllText($_.FullName) -replace "`r?`n", "`r`n"
  [IO.File]::WriteAllText($_.FullName, $text, (New-Object Text.UTF8Encoding($false)))
}

if ($NoZip) { Write-Host "Built $stage"; exit 0 }

New-Item -ItemType Directory -Force -Path (Split-Path $zip) | Out-Null
if (Test-Path $zip) { Remove-Item $zip -Force }
# Zip the folder's contents (not the folder itself) so extraction is flat.
Compress-Archive -Path "$stage\*" -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Wrote $zip ($([math]::Round((Get-Item $zip).Length / 1KB)) KB)"
