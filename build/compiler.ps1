# Compile Moniteur.exe avec le compilateur C# fourni avec Windows (.NET Framework 4.x)
# Usage : .\build\dependances.ps1   (une fois)   puis   .\build\compiler.ps1
# Resultat : bin\Moniteur.exe (toutes les bibliotheques sont incluses dans l'exe)
$ErrorActionPreference = 'Stop'
$racine = Split-Path $PSScriptRoot -Parent
$src = Join-Path $racine 'src'
$deps = Join-Path $PSScriptRoot 'deps'
$bin = Join-Path $racine 'bin'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path "$deps\LibreHardwareMonitorLib.dll")) { throw "Lancez d'abord .\build\dependances.ps1" }
New-Item -ItemType Directory -Force $bin | Out-Null

$ressources = Get-ChildItem $deps -Filter *.dll | Where-Object Name -ne 'System.Management.dll' | ForEach-Object { "/resource:$($_.FullName),$($_.Name)" }
$options = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+',
    "/reference:$deps\LibreHardwareMonitorLib.dll", '/reference:System.Management.dll',
    '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:System.Core.dll',
    "/win32manifest:$src\admin.manifest", "/out:$bin\Moniteur.exe") + $ressources +
    (Get-ChildItem $src -Filter *.cs | ForEach-Object { $_.FullName })
& $csc @options
if ($LASTEXITCODE -ne 0) { throw "La compilation a echoue" }
"OK : $bin\Moniteur.exe"
