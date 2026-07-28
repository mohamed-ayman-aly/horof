<#
.SYNOPSIS
  Builds distributable packages for horof: Windows setup and/or Android APK.

.EXAMPLE
  .\tools\Build-Packages.ps1
  .\tools\Build-Packages.ps1 -Target Windows
  .\tools\Build-Packages.ps1 -Target Android
  .\tools\Build-Packages.ps1 -Target All -Configuration Release
#>
[CmdletBinding()]
param(
    [ValidateSet('All', 'Windows', 'Android')]
    [string] $Target = 'All',

    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    [string] $WindowsRid = 'win-x64'
)

$ErrorActionPreference = 'Stop'

# Prefer the user NuGet cache so builds are not affected by sandbox/temp package caches.
if (-not $env:NUGET_PACKAGES) {
    $env:NUGET_PACKAGES = Join-Path $env:USERPROFILE '.nuget\packages'
}

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$ProjectFile = Join-Path $RepoRoot 'horof\horof.csproj'
$ArtifactsRoot = Join-Path $RepoRoot 'artifacts'
$WindowsArtifacts = Join-Path $ArtifactsRoot 'windows'
$AndroidArtifacts = Join-Path $ArtifactsRoot 'android'
$WindowsPublishDir = Join-Path $WindowsArtifacts 'publish'
$IssFile = Join-Path $PSScriptRoot 'windows\horof.iss'
$KeystorePath = Join-Path $PSScriptRoot 'android\horof.keystore'
$KeystorePropsPath = Join-Path $PSScriptRoot 'android\keystore.props'

$WindowsTfm = 'net10.0-windows10.0.19041.0'
$AndroidTfm = 'net10.0-android'
$AppVersion = '1.0'

function Write-Step([string] $Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-AppVersionFromProject {
    $xml = [xml](Get-Content -LiteralPath $ProjectFile -Raw)
    $version = $xml.Project.PropertyGroup.ApplicationDisplayVersion |
        Where-Object { $_ -and $_.Trim() } |
        Select-Object -First 1
    if ($version) { return $version.Trim() }
    return '1.0'
}

function Find-InnoCompiler {
    $candidates = @(
        (Get-Command 'iscc.exe' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ }

    foreach ($path in $candidates) {
        if (Test-Path -LiteralPath $path) { return $path }
    }
    return $null
}

function Find-Keytool {
    $fromPath = Get-Command 'keytool' -ErrorAction SilentlyContinue
    if ($fromPath) { return $fromPath.Source }

    $searchRoots = @(
        $env:JAVA_HOME,
        "$env:ProgramFiles\Android\Android Studio\jbr",
        "$env:LocalAppData\Programs\Android\Android Studio\jbr",
        "$env:LocalAppData\Android\Sdk\jbr"
    ) | Where-Object { $_ }

    foreach ($root in $searchRoots) {
        $candidate = Join-Path $root 'bin\keytool.exe'
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }

    $jdkDirs = Get-ChildItem "$env:ProgramFiles\Microsoft\jdk*" -Directory -ErrorAction SilentlyContinue
    foreach ($dir in $jdkDirs) {
        $candidate = Join-Path $dir.FullName 'bin\keytool.exe'
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }

    return $null
}

function Ensure-AndroidKeystore {
    if ((Test-Path -LiteralPath $KeystorePath) -and (Test-Path -LiteralPath $KeystorePropsPath)) {
        return
    }

    $keytool = Find-Keytool
    if (-not $keytool) {
        Write-Warning "keytool not found. Android APK will be published without a custom keystore (debug/default signing if available)."
        return
    }

    $androidDir = Split-Path -Parent $KeystorePath
    New-Item -ItemType Directory -Force -Path $androidDir | Out-Null

    $password = 'horof-dev-change-me'
    $alias = 'horof'

    Write-Step "Creating Android signing keystore at tools\android\horof.keystore"
    & $keytool `
        -genkeypair `
        -v `
        -keystore $KeystorePath `
        -alias $alias `
        -keyalg RSA `
        -keysize 2048 `
        -validity 10000 `
        -storepass $password `
        -keypass $password `
        -dname 'CN=horof, OU=Dev, O=horof, L=Local, S=Local, C=US'

    @"
# Auto-generated for local/sideload APK builds. Do not use this keystore for Play Store releases.
AndroidSigningKeyStore=$KeystorePath
AndroidSigningKeyAlias=$alias
AndroidSigningKeyPass=$password
AndroidSigningStorePass=$password
"@ | Set-Content -LiteralPath $KeystorePropsPath -Encoding UTF8
}

function Get-AndroidSigningArgs {
    if (-not (Test-Path -LiteralPath $KeystorePropsPath)) {
        return @()
    }

    $props = @{}
    Get-Content -LiteralPath $KeystorePropsPath |
        Where-Object { $_ -match '^\s*[^#].*=' } |
        ForEach-Object {
            $name, $value = $_ -split '=', 2
            $props[$name.Trim()] = $value.Trim()
        }

    return @(
        "-p:AndroidKeyStore=true",
        "-p:AndroidSigningKeyStore=$($props['AndroidSigningKeyStore'])",
        "-p:AndroidSigningKeyAlias=$($props['AndroidSigningKeyAlias'])",
        "-p:AndroidSigningKeyPass=$($props['AndroidSigningKeyPass'])",
        "-p:AndroidSigningStorePass=$($props['AndroidSigningStorePass'])"
    )
}

function Publish-WindowsPackage {
    Write-Step "Publishing Windows ($WindowsRid, $Configuration)"

    if (Test-Path -LiteralPath $WindowsArtifacts) {
        Remove-Item -LiteralPath $WindowsArtifacts -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $WindowsPublishDir | Out-Null

    & dotnet publish $ProjectFile `
        -f $WindowsTfm `
        -c $Configuration `
        -p:RuntimeIdentifierOverride=$WindowsRid `
        -p:WindowsPackageType=None `
        -p:WindowsAppSDKSelfContained=true `
        -p:PublishReadyToRun=true `
        -o $WindowsPublishDir

    if ($LASTEXITCODE -ne 0) {
        throw "Windows publish failed with exit code $LASTEXITCODE."
    }

    $exePath = Join-Path $WindowsPublishDir 'horof.exe'
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Expected executable not found: $exePath"
    }

    $zipPath = Join-Path $WindowsArtifacts "horof-windows-$WindowsRid-v$AppVersion.zip"
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $WindowsPublishDir '*') -DestinationPath $zipPath -Force
    Write-Host "Created portable zip: $zipPath" -ForegroundColor Green

    $iscc = Find-InnoCompiler
    if ($iscc) {
        Write-Step "Building Windows setup with Inno Setup"
        $setupOut = Join-Path $WindowsArtifacts "horof-setup-$AppVersion.exe"
        & $iscc `
            "/DAppVersion=$AppVersion" `
            "/DSourceDir=$WindowsPublishDir" `
            "/DOutputDir=$WindowsArtifacts" `
            $IssFile

        if ($LASTEXITCODE -ne 0) {
            throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
        }

        if (Test-Path -LiteralPath $setupOut) {
            Write-Host "Created Windows setup: $setupOut" -ForegroundColor Green
        } else {
            $built = Get-ChildItem -LiteralPath $WindowsArtifacts -Filter 'horof-setup-*.exe' | Select-Object -First 1
            if ($built) {
                Write-Host "Created Windows setup: $($built.FullName)" -ForegroundColor Green
            } else {
                Write-Warning "Inno Setup finished but setup executable was not found in $WindowsArtifacts"
            }
        }
    } else {
        Write-Warning @"
Inno Setup (ISCC.exe) was not found. Portable zip was created instead.
Install Inno Setup 6 from https://jrsoftware.org/isinfo.php then re-run:
  .\tools\Build-Packages.ps1 -Target Windows
"@
    }
}

function Publish-AndroidApk {
    Write-Step "Publishing Android APK ($Configuration)"

    if (Test-Path -LiteralPath $AndroidArtifacts) {
        Remove-Item -LiteralPath $AndroidArtifacts -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $AndroidArtifacts | Out-Null

    Ensure-AndroidKeystore
    $signingArgs = Get-AndroidSigningArgs

    & dotnet publish $ProjectFile `
        -f $AndroidTfm `
        -c $Configuration `
        -p:AndroidPackageFormats=apk `
        @signingArgs

    if ($LASTEXITCODE -ne 0) {
        throw "Android publish failed with exit code $LASTEXITCODE."
    }

    $searchRoots = @(
        (Join-Path $RepoRoot "horof\bin\$Configuration\$AndroidTfm\publish"),
        (Join-Path $RepoRoot "horof\bin\$Configuration\$AndroidTfm")
    )

    $apkFiles = @()
    foreach ($root in $searchRoots) {
        if (Test-Path -LiteralPath $root) {
            $apkFiles += Get-ChildItem -LiteralPath $root -Filter '*.apk' -Recurse -ErrorAction SilentlyContinue
        }
    }

    $apkFiles = $apkFiles | Sort-Object LastWriteTime -Descending | Select-Object -Unique -Property FullName, Name, Length, LastWriteTime
    if (-not $apkFiles -or $apkFiles.Count -eq 0) {
        throw "No APK files were produced. Check Android SDK / workload installation."
    }

    $preferred = $apkFiles | Where-Object { $_.Name -match 'Signed' } | Select-Object -First 1
    if (-not $preferred) { $preferred = $apkFiles | Select-Object -First 1 }

    $destName = "horof-v$AppVersion.apk"
    $destPath = Join-Path $AndroidArtifacts $destName
    Copy-Item -LiteralPath $preferred.FullName -Destination $destPath -Force
    Write-Host "Created Android APK: $destPath" -ForegroundColor Green

    foreach ($apk in $apkFiles) {
        if ($apk.FullName -ne $preferred.FullName) {
            Copy-Item -LiteralPath $apk.FullName -Destination (Join-Path $AndroidArtifacts $apk.Name) -Force
        }
    }
}

# --- main ---
if (-not (Test-Path -LiteralPath $ProjectFile)) {
    throw "Project file not found: $ProjectFile"
}

$AppVersion = Get-AppVersionFromProject
New-Item -ItemType Directory -Force -Path $ArtifactsRoot | Out-Null

Write-Host "horof package builder" -ForegroundColor Yellow
Write-Host "Target: $Target | Configuration: $Configuration | Version: $AppVersion"

$sw = [System.Diagnostics.Stopwatch]::StartNew()

if ($Target -in @('All', 'Windows')) {
    Publish-WindowsPackage
}

if ($Target -in @('All', 'Android')) {
    Publish-AndroidApk
}

$sw.Stop()
Write-Host ""
Write-Host "Done in $([math]::Round($sw.Elapsed.TotalSeconds, 1))s. Output: $ArtifactsRoot" -ForegroundColor Green
Get-ChildItem -LiteralPath $ArtifactsRoot -Recurse -File |
    Where-Object { $_.Extension -in '.exe', '.apk', '.zip' } |
    ForEach-Object {
        $mb = [math]::Round($_.Length / 1MB, 2)
        Write-Host ("  - {0} ({1} MB)" -f $_.FullName.Substring($ArtifactsRoot.Length + 1), $mb)
    }
