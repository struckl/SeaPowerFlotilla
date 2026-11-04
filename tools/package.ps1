# Builds the release exe, runs the offline tests and zips the download: dist/SeaPowerFlotilla-<version>.zip
param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'src\Flotilla\Flotilla.csproj'
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

dotnet build $project -c Release --nologo -v q
if ($LASTEXITCODE) { throw 'build failed' }
if (-not $SkipTests) {
    $tests = Join-Path $repo 'tests\Flotilla.Tests\Flotilla.Tests.csproj'
    dotnet build $tests -c Release --nologo -v q
    if ($LASTEXITCODE) { throw 'test build failed' }
    & (Join-Path $repo 'tests\Flotilla.Tests\bin\Release\net48\Flotilla.Tests.exe') $env:TEMP | Select-Object -Last 1
    if ($LASTEXITCODE) { throw 'tests failed' }
}

$out = Join-Path $repo 'src\Flotilla\bin\Release\net48'
$stage = Join-Path $repo "dist\SeaPowerFlotilla"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $stage | Out-Null
Copy-Item (Join-Path $out 'SeaPowerFlotilla.exe'), (Join-Path $out 'SeaPowerFlotilla.exe.config') $stage
Copy-Item (Join-Path $repo 'LICENSE') (Join-Path $stage 'LICENSE.txt')
Copy-Item (Join-Path $repo 'docs\readme-download.txt') (Join-Path $stage 'README.txt')
$zip = Join-Path $repo "dist\SeaPowerFlotilla-$version.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Write-Host "$zip ($([math]::Round((Get-Item $zip).Length / 1KB)) KB)"
