# Prepare le zip a partager : dist\Moniteur-CPU-GPU.zip
# (compile Moniteur.exe, recupere PresentMon.exe officiel, ajoute la notice et les licences)
$ErrorActionPreference = 'Stop'
$racine = Split-Path $PSScriptRoot -Parent
$versionPM = '2.6.0'

if (-not (Test-Path "$PSScriptRoot\deps\LibreHardwareMonitorLib.dll")) { & "$PSScriptRoot\dependances.ps1" | Out-Null }
& "$PSScriptRoot\compiler.ps1" | Out-Null

$pm = Join-Path $PSScriptRoot 'PresentMon.exe'
if (-not (Test-Path $pm)) {
    Invoke-WebRequest "https://github.com/GameTechDev/PresentMon/releases/download/v$versionPM/PresentMon-$versionPM-x64.exe" -OutFile $pm -UseBasicParsing
}
$sig = Get-AuthenticodeSignature $pm
if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'Intel Corporation') { throw "Signature de PresentMon.exe invalide" }

$dist = Join-Path $racine 'dist'
$dossier = Join-Path $dist 'Moniteur CPU GPU'
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force "$dossier\LICENCES" | Out-Null
Copy-Item "$racine\bin\Moniteur.exe" $dossier
Copy-Item $pm "$dossier\PresentMon.exe"
Copy-Item "$PSScriptRoot\LISEZMOI.txt" $dossier
Copy-Item "$racine\LICENSE" "$dossier\LICENCES\LICENSE.txt"
Copy-Item "$racine\THIRD-PARTY-NOTICES.md" "$dossier\LICENCES\THIRD-PARTY-NOTICES.md"
Copy-Item "$PSScriptRoot\licences\*" "$dossier\LICENCES\"

$zip = Join-Path $dist 'Moniteur-CPU-GPU.zip'
Compress-Archive -Path $dossier -DestinationPath $zip -Force
Get-ChildItem $dossier -Recurse -File | ForEach-Object { $_.FullName.Substring($dossier.Length + 1) }
"Zip : $zip ({0:N1} Mo)" -f ((Get-Item $zip).Length / 1MB)
