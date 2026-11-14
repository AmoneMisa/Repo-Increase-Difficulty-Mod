param(
    [string]$OutputDirectory,
    # DeepRunOverhaul (default) or IncreaseDifficultyMod: the same full mod published under its old package name,
    # for when the renamed package cannot go up yet. Plugin, GUID and version are identical; only the package
    # metadata differs (and the README/changelog drop the "this package moved" notes).
    [ValidateSet('DeepRunOverhaul', 'IncreaseDifficultyMod')]
    [string]$Name = 'DeepRunOverhaul'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'bin\thunderstore' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$manifestPath = Join-Path $repo 'manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.name -cne 'DeepRunOverhaul') { throw 'Incorrect package name.' }
if ($manifest.version_number -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid package version.' }
if ($manifest.description.Length -gt 250) { throw 'Description exceeds 250 characters.' }
if (-not $manifest.website_url -or $manifest.dependencies.Count -lt 2) { throw 'Incomplete manifest.' }
foreach ($dependency in $manifest.dependencies) {
    if ($dependency -notmatch '^[A-Za-z0-9_]+-[A-Za-z0-9_]+-\d+\.\d+\.\d+$') {
        throw "Invalid dependency: $dependency"
    }
}
$project = Join-Path $repo 'IncreaseDifficultyMod.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
if ($projectXml.Project.PropertyGroup.Version -ne $manifest.version_number) {
    throw 'Project version does not match manifest.'
}
Add-Type -AssemblyName System.Drawing
$icon = [Drawing.Image]::FromFile((Join-Path $repo 'icon.png'))
try {
    if ($icon.Width -ne 256 -or $icon.Height -ne 256 -or
        $icon.RawFormat.Guid -ne [Drawing.Imaging.ImageFormat]::Png.Guid) {
        throw 'icon.png must be a 256x256 PNG.'
    }
} finally { $icon.Dispose() }

& dotnet build $project -c Release -p:DeployToGame=false -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
$dll = Join-Path $repo 'bin\Release\DeepRunOverhaul.dll'
$assembly = [Reflection.AssemblyName]::GetAssemblyName($dll)
if ($assembly.Name -cne $manifest.name -or $assembly.Version.ToString(3) -ne $manifest.version_number) {
    throw 'Plugin identity/version does not match manifest.'
}

# Thunderstore requires UTF-8 metadata without a BOM, including the changelog.
$utf8 = [Text.UTF8Encoding]::new($false, $true)
function Read-Utf8([string]$path) {
    $bytes = [IO.File]::ReadAllBytes($path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $bytes = [byte[]]$bytes[3..($bytes.Length - 1)]
    }
    return $utf8.GetString($bytes)
}
$manifestText = Read-Utf8 $manifestPath
$readmeText = Read-Utf8 (Join-Path $repo 'README.md')
$changelogText = Read-Utf8 (Join-Path $repo "release-notes\v$($manifest.version_number).md")
$plugin = 'plugins/DeepRunOverhaul/DeepRunOverhaul.dll'
if ($Name -ceq 'IncreaseDifficultyMod') {
    $package = [ordered]@{
        name = 'IncreaseDifficultyMod'
        version_number = $manifest.version_number
        website_url = $manifest.website_url
        description = ($manifest.description -replace '\s*Formerly IncreaseDifficultyMod\.?$', ' Also published as DeepRunOverhaul.')
        dependencies = @($manifest.dependencies)
    }
    if ($package.description.Length -gt 250) { throw 'Description exceeds 250 characters.' }
    $manifestText = $package | ConvertTo-Json -Depth 3
    # Notes that only make sense on the renamed package ("this package moved") are left out.
    # ASCII-only patterns: Windows PowerShell 5.1 reads this file in the ANSI code page.
    $drop = '^\*\*(Moving from IncreaseDifficultyMod|\S+ \S+ IncreaseDifficultyMod:|New name\.)'
    $readmeText = (($readmeText -split "`n") | Where-Object { $_ -notmatch $drop }) -join "`n"
    $readmeText = $readmeText -replace '(?m)^# DeepRunOverhaul\r?$', '# IncreaseDifficultyMod'
    $readmeText = $readmeText -replace '(?m)^\*Formerly IncreaseDifficultyMod / .*$', '*Also published as DeepRunOverhaul.*'
    $changelogText = (($changelogText -split "`n") | Where-Object { $_ -notmatch $drop }) -join "`n"
    $changelogText = $changelogText -replace '(?m)^# DeepRunOverhaul ', '# IncreaseDifficultyMod '
    $plugin = 'plugins/IncreaseDifficultyMod/DeepRunOverhaul.dll'
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$zipPath = Join-Path $OutputDirectory "$Name-$($manifest.version_number).zip"
# Create explicitly named ZIP entries: required metadata must be at the archive root.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$files = [ordered]@{
    'manifest.json' = $manifestText
    'README.md' = $readmeText
    'icon.png' = (Join-Path $repo 'icon.png')
    'CHANGELOG.md' = $changelogText
    'COLLECTION.md' = (Read-Utf8 (Join-Path $repo 'art\valuables\COLLECTION_IN_GAME.md'))
    $plugin = $dll
}
$stream = [IO.File]::Open($zipPath, [IO.FileMode]::Create, [IO.FileAccess]::ReadWrite)
$zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($entry in $files.GetEnumerator()) {
        if ($entry.Key -match '\.(md|json)$') {
            $bytes = $utf8.GetBytes($entry.Value)
            $zipEntry = $zip.CreateEntry($entry.Key, [IO.Compression.CompressionLevel]::Optimal)
            $entryStream = $zipEntry.Open()
            try { $entryStream.Write($bytes, 0, $bytes.Length) } finally { $entryStream.Dispose() }
        } else {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $entry.Value, $entry.Key, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
} finally { $zip.Dispose(); $stream.Dispose() }
$check = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $entries = @($check.Entries | ForEach-Object FullName)
    if ($entries.Count -ne $files.Count) { throw 'Unexpected ZIP entry count.' }
    foreach ($key in $files.Keys) {
        if ($entries -cnotcontains $key) { throw "Missing ZIP entry: $key" }
    }
    $entries | ForEach-Object { Write-Output "ZIP: $_" }
} finally { $check.Dispose() }
Get-Item -LiteralPath $zipPath | Select-Object FullName, Length
Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
