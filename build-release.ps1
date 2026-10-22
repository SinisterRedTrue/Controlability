<#
.SYNOPSIS
    Builds the Controlability installer: clean -> test -> publish -> Inno Setup -> checksum.

.DESCRIPTION
    Output: artifacts\Controlability-Setup-<version>.exe and artifacts\Controlability-Setup-<version>.exe.sha256
    The version comes from <Version> in Directory.Build.props.

.PARAMETER IsccPath
    Full path of ISCC.exe. Default: the ISCC_PATH environment variable, the Inno Setup 6 uninstall entry, Program Files, then PATH.

.PARAMETER SkipTests
    Skips "dotnet test". For quick local packaging only; a release build should run the tests.
#>
[CmdletBinding()]
param(
    [string]$IsccPath,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

# The repo path contains "[ ]" and spaces: every path below is quoted or passed with -LiteralPath.
$root = $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$publishDir = Join-Path $artifacts 'publish'
$solution = Join-Path $root 'Controlability.sln'
$appProject = Join-Path $root 'src\Controlability.App'
$script = Join-Path $root 'installer\Controlability.iss'

function Invoke-Step([string]$Name, [scriptblock]$Action) {
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed (exit code $LASTEXITCODE)."
    }
}

function Find-Iscc {
    if ($IsccPath) {
        if (-not (Test-Path -LiteralPath $IsccPath)) { throw "ISCC.exe not found at '$IsccPath'." }
        return $IsccPath
    }

    $candidates = @($env:ISCC_PATH)
    foreach ($key in 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
                     'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
                     'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1') {
        $item = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
        if ($item -and $item.InstallLocation) { $candidates += Join-Path $item.InstallLocation 'ISCC.exe' }
    }
    $candidates += Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
    $candidates += Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'
    $candidates += Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) { return $candidate }
    }

    $onPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    throw 'ISCC.exe (Inno Setup 6) not found. Install it from https://jrsoftware.org/isdl.php or pass -IsccPath.'
}

# One version, from Directory.Build.props. "1.0.0-rc1" -> file version "1.0.0.0".
[xml]$props = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
$version = ($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw '<Version> not found in Directory.Build.props.' }
if ($version -notmatch '^(\d+)\.(\d+)\.(\d+)') { throw "Unsupported version '$version'." }
$fileVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"
Write-Host "Controlability $version (file version $fileVersion)"

$iscc = Find-Iscc
Write-Host "Inno Setup: $iscc"

Push-Location -LiteralPath $root
try {
    Invoke-Step 'Clean' {
        if (Test-Path -LiteralPath $artifacts) { Remove-Item -LiteralPath $artifacts -Recurse -Force }
        dotnet clean $solution -c Release --nologo -v quiet
    }

    if ($SkipTests) {
        Write-Warning 'Tests skipped.'
    } else {
        Invoke-Step 'Test' { dotnet test $solution -c Release --nologo }
    }

    Invoke-Step 'Publish' {
        dotnet publish $appProject -c Release -r win-x64 --self-contained true `
            -p:PublishSingleFile=false -p:PublishTrimmed=false --nologo -o $publishDir
    }

    foreach ($required in 'Controlability.exe', 'SDL3.dll', 'profiles\generic.json') {
        if (-not (Test-Path -LiteralPath (Join-Path $publishDir $required))) {
            throw "Publish output is missing $required."
        }
    }

    Invoke-Step 'Installer (ISCC)' {
        & $iscc "/DAppVersion=$version" "/DAppFileVersion=$fileVersion" "/DPublishDir=$publishDir" "/DOutputDir=$artifacts" $script
    }

    $setup = Join-Path $artifacts "Controlability-Setup-$version.exe"
    if (-not (Test-Path -LiteralPath $setup)) { throw "Expected installer not found: $setup" }

    Write-Host '==> Checksum' -ForegroundColor Cyan
    $hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
    # sha256sum format ("<hash> *<file>"), so "sha256sum -c" works too.
    [System.IO.File]::WriteAllText("$setup.sha256", "$hash *$(Split-Path -Leaf $setup)`n", [System.Text.UTF8Encoding]::new($false))

    Write-Host ''
    Write-Host "Installer: $setup" -ForegroundColor Green
    Write-Host "SHA-256:   $hash"
}
finally {
    Pop-Location
}
