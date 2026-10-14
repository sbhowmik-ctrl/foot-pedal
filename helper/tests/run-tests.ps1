$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpf = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF"
$out = Join-Path $here 'MapperTest.exe'
& $csc /nologo /target:exe /out:$out `
  /reference:System.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
  /reference:"$wpf\WindowsBase.dll" `
  /reference:"$wpf\UIAutomationTypes.dll" `
  /reference:"$wpf\UIAutomationClient.dll" `
  "$here\MapperTest.cs" "$here\..\src\Mapper.cs" "$here\..\src\KeySender.cs" "$here\..\src\MediaSeeker.cs"
if ($LASTEXITCODE -ne 0) { throw 'compile failed' }
& $out
$code = $LASTEXITCODE
Remove-Item $out -ErrorAction SilentlyContinue
exit $code
