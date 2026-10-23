# Regenerates src/data/Ara3D.StepBrep: the C# that Plato's compiler writes from the Plato STEP B-rep library
# (bim-open-toolkit sandbox/step-brep-mesh, layers 00 to 08) and the stdlib tiers it builds on, plus a copy of
# Plato's handwritten runtime (src/Plato.Intrinsics) that the generated structs fuse with.
#
#   tools/regen-step-brep.ps1 [-PlatoCommit <commit>] [-LibraryCommit <commit>] [-PlatoRoot <plato clone>]
#                             [-LibraryRoot <sandbox/step-brep-mesh in a bim-open-toolkit clone>] [-OutputRoot <folder>]
#                             [-CompilerFlags <flag>...] [-Loops]
#   tools/regen-step-brep.ps1 -ProvenanceOnly     prints the header line the files would get, and stops
#
# The compiler's optimizer flags (--loops, --inline, --optimize, --optimize-arrays) are off unless given: -CompilerFlags
# replaces the pinned list $PinnedFlags below (the flags the committed files were generated with, moved with the pin),
# -Loops appends --loops to it. The header line of every generated file names the flags used, so a reader can tell a
# plain build from a lowered one (TKT-505).
#
# Every input comes from a commit, never from a working tree, so uncommitted edits in either clone change nothing and
# a fresh clone writes the same bytes. The compiler, the stdlib tiers and the runtime are Plato at -PlatoCommit (default
# $PinnedPlato below: move the pin here, in one place); the layers are the library clone at -LibraryCommit (default
# HEAD). The compiler is built once per Plato commit from a `git archive` export with its parakeet submodule, kept under
# %TEMP%\regen-step-brep\plato-<commit>; Parakeet finds Ara3D.Utils and Ara3D.Logging through a sibling ara3d-sdk
# checkout, so the export's references that point outside it are rewritten to where they point from PlatoRoot.
#
# Before writing anything, the library's tools/check_generated.py (from the same commit as the layers) scans the
# compiler's output and log; a degraded body, or a throwing stub not listed in tools/generated-stubs.txt, stops
# the run with a non-zero exit (TKT-433).
#
# Inside the toolkit (this repository at bim-open-toolkit/deps/bim-open-data) the roots default to ..\..\..\plato and
# ..\..\sandbox\step-brep-mesh; anywhere else both are required. OutputRoot defaults to src/data/Ara3D.StepBrep; the
# test Ara3D.Step.Decode.Tests.StepBrepGenerationTests passes a temporary folder and compares.
#
# Every file gets one header line naming the Plato commit (the compiler's, which is also the stdlib's and the
# runtime's) and the last bim-open-toolkit commit at or before -LibraryCommit that touched the library layers. Text is
# written with CRLF line ends, as a Windows checkout with core.autocrlf has it. The namespace Ara3D.Geometry that Plato
# writes becomes Ara3D.StepBrep, so a project can reference this one and the Ara3D.Geometry package together (TKT-311
# found CS0433 between the two Point3D types).
param(
    [string]$PlatoCommit,
    [string]$LibraryCommit = 'HEAD',
    [string]$PlatoRoot,
    [string]$LibraryRoot,
    [string]$OutputRoot,
    [string[]]$CompilerFlags,
    [switch]$Loops,
    [switch]$ProvenanceOnly
)

$ErrorActionPreference = 'Stop'
# The Plato commit the committed files are generated from: 60134758 (TKT-479, TKT-445, TKT-446, TKT-447; the
# generated At(index) of TriangleFace and QuadFace faults on an out-of-range corner), the commit the law server runs.
$PinnedPlato = '6013475813a711a70d8a91a80996d8254abd2b33'
if (-not $PlatoCommit) { $PlatoCommit = $PinnedPlato }
# The optimizer flags the committed files are generated with (none as of 2026-10-23); see -CompilerFlags above.
$PinnedFlags = @()
if ($null -eq $CompilerFlags) { $CompilerFlags = $PinnedFlags }
if ($Loops -and $CompilerFlags -notcontains '--loops') { $CompilerFlags += '--loops' }
$flagsNote = if ($CompilerFlags.Count -gt 0) { "; flags $($CompilerFlags -join ' ')" } else { '' }

$repo = Split-Path -Parent $PSScriptRoot
$insideToolkit = (Split-Path -Leaf (Split-Path -Parent $repo)) -eq 'deps'
if (-not $PlatoRoot) {
    if (-not $insideToolkit) { throw '-PlatoRoot is required outside bim-open-toolkit/deps' }
    $PlatoRoot = Join-Path $repo '..\..\..\plato'
}
if (-not $LibraryRoot) {
    if (-not $insideToolkit) { throw '-LibraryRoot is required outside bim-open-toolkit/deps' }
    $LibraryRoot = Join-Path $repo '..\..\sandbox\step-brep-mesh'
}
if (-not $OutputRoot) { $OutputRoot = Join-Path $repo 'src\data\Ara3D.StepBrep' }
$PlatoRoot = (Resolve-Path $PlatoRoot).Path
$LibraryRoot = (Resolve-Path $LibraryRoot).Path

# The program compiled (stdlib tiers, excluded stdlib file names, library layers) is the library's
# tools/shipped-program.txt at the library commit, the one list the law runner and compile-layers-00-08.sh read too.
# The stub check and its baseline come from the same commit.
$programList = 'tools/shipped-program.txt'
$checkFiles = @('tools/check_generated.py', 'tools/generated-stubs.txt')
$fromNamespace = 'Ara3D.Geometry'
$toNamespace = 'Ara3D.StepBrep'

# git with line ends as stored, so an export does not depend on the machine's core.autocrlf.
function Invoke-Git([string]$root, [string[]]$arguments) {
    $result = & git -C $root -c core.autocrlf=false -c core.eol=lf @arguments
    if ($LASTEXITCODE -ne 0) { throw "git -C $root $($arguments -join ' ') failed ($LASTEXITCODE)" }
    $result
}

function Resolve-Commit([string]$root, [string]$commit) { (Invoke-Git $root @('rev-parse', '--verify', "$commit^{commit}")) | Select-Object -First 1 }

# Extracts `git archive <treeish> [paths]` from the clone at root into folder.
function Export-Tree([string]$root, [string]$treeish, [string]$folder, [string[]]$paths = @()) {
    New-Item -ItemType Directory -Force $folder | Out-Null
    $tar = "$folder.tar"
    Invoke-Git $root (@('archive', '--format=tar', "--output=$tar", $treeish) + $paths) | Out-Null
    # Windows' own bsdtar: a GNU tar on the path (Git for Windows) reads "C:" in an archive name as a remote host.
    $tarExe = if ($env:SystemRoot -and (Test-Path "$env:SystemRoot\System32\tar.exe")) { "$env:SystemRoot\System32\tar.exe" } else { 'tar' }
    & $tarExe -xf $tar -C $folder
    if ($LASTEXITCODE -ne 0) { throw "tar -xf $tar failed ($LASTEXITCODE)" }
    Remove-Item $tar
}

function Get-PlatoFiles([string]$dir, [string[]]$excluded) {
    Get-ChildItem (Join-Path $dir '*.plato') -File | Where-Object { $name = $_.Name; -not ($excluded | Where-Object { $name -like $_ }) }
}

function Write-Text([string]$path, [string]$text) {
    $crlf = ($text -replace "`r`n", "`n") -replace "`n", "`r`n"
    [System.IO.File]::WriteAllText($path, $crlf, (New-Object System.Text.UTF8Encoding $false))
}

$platoSha = Resolve-Commit $PlatoRoot $PlatoCommit
$librarySha = Resolve-Commit $LibraryRoot $LibraryCommit
$libraryTop = (Invoke-Git $LibraryRoot @('rev-parse', '--show-toplevel')) | Select-Object -First 1
$libraryPrefix = ((Invoke-Git $LibraryRoot @('rev-parse', '--show-prefix')) | Select-Object -First 1).TrimEnd('/')

# shipped-program.txt: "stdlib <tier>", "exclude <glob>...", "layer <folder>"; # starts a comment.
$stdlibTiers = @(); $stdlibExcluded = @(); $layers = @()
foreach ($line in (Invoke-Git $libraryTop @('show', "${librarySha}:$libraryPrefix/$programList"))) {
    $words = @(("$line" -replace '#.*$', '').Trim() -split '\s+' | Where-Object { $_ })
    if ($words.Count -lt 2) { continue }
    switch ($words[0]) {
        'stdlib' { $stdlibTiers += $words[1] }
        'exclude' { $stdlibExcluded += $words[1..($words.Count - 1)] }
        'layer' { $layers += $words[1] }
        default { throw "$programList at ${librarySha}: unknown line '$line'" }
    }
}
if (-not $stdlibTiers -or -not $layers) { throw "$programList at $librarySha names no stdlib tier or no layer" }
$layerRange = "layers $($layers[0].Substring(0, 2)) to $($layers[-1].Substring(0, 2))"

$libraryTouched = (Invoke-Git $libraryTop (@('log', '-1', '--format=%h', $librarySha, '--') + ($layers | ForEach-Object { "$libraryPrefix/$_" }))) | Select-Object -First 1
if (-not $libraryTouched) { throw "no commit at or before $librarySha touches $libraryPrefix $layerRange" }
$header = "// Generated by tools/regen-step-brep.ps1 from Plato $($platoSha.Substring(0, 8)) and bim-open-toolkit $libraryTouched (sandbox/step-brep-mesh $layerRange$flagsNote). Do not edit."
if ($ProvenanceOnly) {
    Write-Output $header
    return
}
$header += "`n"

$clock = [System.Diagnostics.Stopwatch]::StartNew()
function Write-Step([string]$text) { Write-Host ("[{0,5:N1} s] {1}" -f $clock.Elapsed.TotalSeconds, $text) }

# Plato at the commit, with its parakeet submodule, built once and kept.
$cacheRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'regen-step-brep'
$platoExport = Join-Path $cacheRoot "plato-$($platoSha.Substring(0, 12))"
if (-not (Test-Path (Join-Path $platoExport '.exported'))) {
    Write-Step "exporting Plato $platoSha to $platoExport"
    $partial = "$platoExport.partial-$PID"
    if (Test-Path $partial) { Remove-Item -Recurse -Force $partial }
    Export-Tree $PlatoRoot $platoSha $partial
    $submodule = (Invoke-Git $PlatoRoot @('ls-tree', $platoSha, 'parakeet')) | Select-Object -First 1
    if ($submodule -notmatch '^160000 commit ([0-9a-f]{40})\s') { throw "Plato $platoSha has no parakeet submodule entry" }
    Export-Tree (Join-Path $PlatoRoot 'parakeet') $Matches[1] (Join-Path $partial 'parakeet')
    foreach ($project in Get-ChildItem $partial -Recurse -Filter *.csproj -File) {
        $original = Join-Path $PlatoRoot $project.FullName.Substring($partial.Length + 1)
        $text = [System.IO.File]::ReadAllText($project.FullName)
        $rewritten = [regex]::Replace($text, '<ProjectReference Include="(\.\.[^"]*)"', {
            param($m)
            $target = [System.IO.Path]::GetFullPath((Join-Path $project.DirectoryName $m.Groups[1].Value))
            if ($target.StartsWith($partial + '\', [StringComparison]::OrdinalIgnoreCase)) { return $m.Value }
            $outside = [System.IO.Path]::GetFullPath((Join-Path (Split-Path $original) $m.Groups[1].Value))
            "<ProjectReference Include=`"$outside`""
        })
        if ($rewritten -ne $text) { [System.IO.File]::WriteAllText($project.FullName, $rewritten) }
    }
    New-Item -ItemType File (Join-Path $partial '.exported') | Out-Null
    if (Test-Path $platoExport) { Remove-Item -Recurse -Force $partial } else { Rename-Item $partial (Split-Path -Leaf $platoExport) }
}
$outside = Get-ChildItem $platoExport -Recurse -Filter *.csproj -File | Select-String -Pattern 'Include="([A-Za-z]:\\[^"]*)"' -AllMatches |
    ForEach-Object { $_.Matches | ForEach-Object { $_.Groups[1].Value } } | ForEach-Object { (Invoke-Git (Split-Path $_) @('rev-parse', '--show-toplevel')) | Select-Object -First 1 } | Sort-Object -Unique
foreach ($top in $outside) { Write-Step "references outside the export: $top at $((Invoke-Git $top @('log', '-1', '--format=%h')) | Select-Object -First 1)" }

$cliBuild = "$platoExport-build"
$cli = Get-ChildItem $cliBuild -Recurse -Filter Plato.CLI.dll -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match '\\bin\\' } | Select-Object -First 1
if (-not $cli) {
    $cliProject = Join-Path $platoExport 'src\Plato.CLI\Plato.CLI.csproj'
    Write-Step "dotnet build $cliProject -c Release --artifacts-path $cliBuild -nodeReuse:false -m:1"
    $buildLog = & dotnet build $cliProject -c Release --artifacts-path $cliBuild -nodeReuse:false -m:1 2>&1
    if ($LASTEXITCODE -ne 0) { $buildLog | Where-Object { "$_" -match ' error ' } | Select-Object -First 20 | Write-Host; throw "building Plato.CLI failed ($LASTEXITCODE)" }
    $cli = Get-ChildItem $cliBuild -Recurse -Filter Plato.CLI.dll | Where-Object { $_.FullName -match '\\bin\\' } | Select-Object -First 1
    if (-not $cli) { throw "Plato.CLI.dll not found under $cliBuild" }
}
Write-Step "compiler $($cli.FullName)"

$stage = Join-Path $cacheRoot ('run-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $stage | Out-Null
try {
    $inputs = @()
    foreach ($tier in $stdlibTiers) {
        $dir = Join-Path $stage "in\$tier"
        New-Item -ItemType Directory -Force $dir | Out-Null
        Get-PlatoFiles (Join-Path $platoExport "stdlib\$tier") $stdlibExcluded | Copy-Item -Destination $dir
        $inputs += $dir
    }
    $libraryExport = Join-Path $stage 'library'
    Export-Tree $libraryTop "${librarySha}:$libraryPrefix" $libraryExport ($layers + $checkFiles)
    $libraryInput = Join-Path $stage 'in\library'
    New-Item -ItemType Directory -Force $libraryInput | Out-Null
    foreach ($layer in $layers) {
        Get-PlatoFiles (Join-Path $libraryExport $layer) @('*.spec.plato') | Copy-Item -Destination $libraryInput
    }
    $inputs += $libraryInput

    $out = Join-Path $stage 'out'
    New-Item -ItemType Directory -Force $out | Out-Null
    $arguments = @($cli.FullName) + $inputs + @("--out=$out", '--profile=cpu64') + $CompilerFlags
    Write-Step ("dotnet " + ($arguments -join ' '))
    # Windows PowerShell 5.1 turns each stderr line of a native command into an ErrorRecord, which under
    # $ErrorActionPreference = 'Stop' ends the script before the log is shown; the compiler writes its errors there.
    $log = & { $ErrorActionPreference = 'Continue'; & dotnet @arguments 2>&1 | ForEach-Object { "$_" } }
    if ($LASTEXITCODE -ne 0) { $log | Select-Object -Last 30 | Write-Host; throw "Plato.CLI failed ($LASTEXITCODE)" }
    $log | Where-Object { "$_" -match 'DEGRADED|degraded:' } | Write-Host

    # Nothing is written while the compiler's output holds a degraded body or a throwing stub that
    # generated-stubs.txt does not list (TKT-433; TKT-410's one DEGRADED line cost the corpus 22,410 faces).
    $compilerLog = Join-Path $stage 'compiler.log'
    Write-Text $compilerLog ($log -join "`n")
    $check = Join-Path $libraryExport $checkFiles[0]
    Write-Step "python -I $check $out --log $compilerLog"
    $checkLog = & { $ErrorActionPreference = 'Continue'; & python -I $check $out --log $compilerLog 2>&1 | ForEach-Object { "$_" } }
    $checkLog | Write-Host
    if ($LASTEXITCODE -ne 0) { throw "check_generated.py failed ($LASTEXITCODE): the generated C# is not written" }

    $generated = Join-Path $OutputRoot 'Generated'
    $intrinsics = Join-Path $OutputRoot 'Intrinsics'
    foreach ($dir in @($generated, $intrinsics)) {
        if (Test-Path $dir) { Get-ChildItem $dir -Filter *.cs -File | Remove-Item }
        New-Item -ItemType Directory -Force $dir | Out-Null
    }
    $files = Get-ChildItem $out -Filter *.g.cs -File
    foreach ($f in $files) {
        Write-Text (Join-Path $generated $f.Name) ($header + [System.IO.File]::ReadAllText($f.FullName).Replace($fromNamespace, $toNamespace))
    }
    $runtime = Join-Path $platoExport 'src\Plato.Intrinsics'
    $runtimeFiles = ([xml](Get-Content (Join-Path $runtime 'Plato.Intrinsics.projitems'))).Project.ItemGroup.Compile.Include |
        ForEach-Object { $_.Replace('$(MSBuildThisFileDirectory)', '') }
    foreach ($name in $runtimeFiles) {
        Write-Text (Join-Path $intrinsics $name) ($header + [System.IO.File]::ReadAllText((Join-Path $runtime $name)).Replace($fromNamespace, $toNamespace))
    }
    Write-Step "wrote $($files.Count) generated files to $generated and $($runtimeFiles.Count) runtime files to $intrinsics"
    Write-Host "Plato $platoSha (compiler, stdlib, runtime); bim-open-toolkit $librarySha (layers last changed in $libraryTouched); profile cpu64$flagsNote"
}
finally {
    Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
}
