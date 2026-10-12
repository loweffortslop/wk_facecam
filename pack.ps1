# Builds FaceCam and zips a Thunderstore package into dist/.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$ts = Join-Path $root 'thunderstore'

# The Thunderstore page text (README.md, manifest description, CHANGELOG.md) is written by hand.
$manifest = Get-Content "$ts\manifest.json" -Raw | ConvertFrom-Json
$problems = @()
if ((Get-Content "$ts\README.md" -Raw) -match 'TODO') { $problems += 'thunderstore\README.md still has the TODO placeholder' }
if ($manifest.description -match 'TODO') { $problems += 'manifest.json description still has the TODO placeholder' }
if ($manifest.description.Length -gt 250) { $problems += "manifest.json description is $($manifest.description.Length) characters (max 250)" }
if ($problems) { $problems | ForEach-Object { Write-Host "Not packaging: $_" -ForegroundColor Red }; exit 1 }

dotnet build "$root\FaceCam.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$version = $manifest.version_number
$stage = Join-Path $root 'dist\stage'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force "$stage\plugins" | Out-Null

Copy-Item "$ts\manifest.json", "$ts\README.md", "$ts\icon.png" $stage
if (Test-Path "$ts\CHANGELOG.md") { Copy-Item "$ts\CHANGELOG.md" $stage }
Copy-Item "$root\bin\Release\FaceCam.dll" "$stage\plugins"

$zip = Join-Path $root "dist\SlopGaming-FaceCam-$version.zip"
if (Test-Path $zip) { Remove-Item $zip }
# Not Compress-Archive: on Windows PowerShell 5.1 it stores entries with backslashes (plugins\FaceCam.dll).
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    Get-ChildItem $stage -Recurse -File | ForEach-Object {
        $entry = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entry) | Out-Null
    }
} finally { $archive.Dispose() }
Remove-Item $stage -Recurse -Force
Write-Host "Packaged $zip"
