$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$references = @('System.dll','System.Core.dll','System.Data.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xml.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ ('/out:' + (Join-Path $PSScriptRoot 'Lumen.exe')) ('/win32manifest:' + (Join-Path $PSScriptRoot 'source\app.manifest')) $references (Join-Path $PSScriptRoot 'source\Lumen.cs') (Join-Path $PSScriptRoot 'source\Startup.cs') (Join-Path $PSScriptRoot 'source\SettingsCatalog.cs')
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output 'Built Lumen.exe'

