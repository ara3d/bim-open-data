# Downloads the STEP long-form EXPRESS schemas (AP242 edition 3, AP214 edition 3, AP203 edition 2)
# from the MBx Interoperability Forum's schema page (https://www.mbx-if.org/home/?p=613) into
# data/express/<name>/. The page states no licence, so the files are never committed
# (.gitignore excludes data/** except the fetch scripts and README.md). The STEP type generator
# (tools/Ara3D.IfcTypeGen) reads them from here.
param([string]$Destination = (Join-Path $PSScriptRoot 'express'))

$ErrorActionPreference = 'Stop'
$base = 'https://www.mbx-if.org/home/wp-content/uploads/2024/07'
$schemas = [ordered]@{
    'ap242ed3' = 'ap242ed3_mim_lf_v1.152.zip'
    'ap214ed3' = 'AP214E3_2010.zip'
    'ap203ed2' = 'part403ts_wg3n2635mim_lf.zip'
}

foreach ($name in $schemas.Keys) {
    $dir = Join-Path $Destination $name
    if (Get-ChildItem $dir -Filter *.exp -Recurse -ErrorAction SilentlyContinue) {
        Write-Host "present: $name"
        continue
    }
    New-Item -ItemType Directory -Force $dir | Out-Null
    $zip = Join-Path $dir $schemas[$name]
    Invoke-WebRequest "$base/$($schemas[$name])" -OutFile $zip
    Expand-Archive $zip -DestinationPath $dir -Force
    Remove-Item $zip
    Get-ChildItem $dir -Filter *.exp -Recurse | ForEach-Object { Write-Host "fetched: $($_.FullName) ($($_.Length) bytes)" }
}
