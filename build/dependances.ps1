# Telecharge LibreHardwareMonitorLib et ses dependances (.NET Framework) depuis nuget.org
# Resultat : build\deps\*.dll
$ErrorActionPreference = 'Stop'
$racine = Join-Path $PSScriptRoot 'deps'
$out = $racine
New-Item -ItemType Directory -Force $out | Out-Null
$cache = Join-Path $PSScriptRoot 'paquets'
$vus = @{}
$tfms = 'net472','net471','net47','net462','net461','net46','net45','netstandard2.0','netstandard1.3'

function Get-Paquet($id, $ver) {
    $cle = $id.ToLower()
    if ($vus[$cle]) { return }; $vus[$cle] = $true
    $ver = ($ver -replace '[\[\]\(\)]', '').Split(',')[0].ToLower()
    $dir = Join-Path $cache $cle
    $zip = "$dir.zip"
    New-Item -ItemType Directory -Force $cache | Out-Null
    Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$cle/$ver/$cle.$ver.nupkg" -OutFile $zip -UseBasicParsing
    Expand-Archive $zip $dir -Force
    [xml]$ns = Get-Content (Get-ChildItem $dir -Filter *.nuspec | Select-Object -First 1).FullName
    $choisi = $null
    foreach ($t in $tfms) {
        foreach ($base in @("runtimes\win-x64\lib\$t", "runtimes\win\lib\$t", "lib\$t")) {
            $p = Join-Path $dir $base
            if ((Test-Path $p) -and (Get-ChildItem $p -Filter *.dll)) { $choisi = $p; break }
        }
        if ($choisi) { break }
    }
    if ($choisi) { Copy-Item "$choisi\*.dll" $out -Force; "$id $ver" }
    $groupes = @($ns.package.metadata.dependencies.group)
    foreach ($t in @('.NETFramework4.7.2','.NETFramework4.6.2','.NETFramework4.6.1','.NETFramework4.6','.NETFramework4.5','.NETStandard2.0')) {
        $g = $groupes | Where-Object { $_.targetFramework -eq $t } | Select-Object -First 1
        if ($g) { foreach ($d in @($g.dependency)) { if ($d) { Get-Paquet $d.id $d.version } }; break }
    }
}

Get-Paquet 'LibreHardwareMonitorLib' '0.9.6'
