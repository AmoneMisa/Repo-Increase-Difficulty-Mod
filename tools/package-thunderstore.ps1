param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'bin\thunderstore' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$manifestPath = Join-Path $repo 'manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.name -cne 'IncreaseDifficultyMod') { throw 'Incorrect package name.' }
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
$dll = Join-Path $repo 'bin\Release\IncreaseDifficultyMod.dll'
$assembly = [Reflection.AssemblyName]::GetAssemblyName($dll)
if ($assembly.Name -cne $manifest.name -or $assembly.Version.ToString(3) -ne $manifest.version_number) {
    throw 'Plugin identity/version does not match manifest.'
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$zipPath = Join-Path $OutputDirectory "$($manifest.name)-$($manifest.version_number).zip"
# Create explicitly named ZIP entries: required metadata must be at the archive root.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream = [IO.File]::Open($zipPath, [IO.FileMode]::Create, [IO.FileAccess]::ReadWrite)
$zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    $files = [ordered]@{
        'manifest.json' = $manifestPath
        'README.md' = (Join-Path $repo 'README.md')
        'icon.png' = (Join-Path $repo 'icon.png')
        'CHANGELOG.md' = (Join-Path $repo "release-notes\v$($manifest.version_number).md")
        'COLLECTION.md' = (Join-Path $repo 'art\valuables\COLLECTION_IN_GAME.md')
        'plugins/IncreaseDifficultyMod/IncreaseDifficultyMod.dll' = $dll
    }
    foreach ($entry in $files.GetEnumerator()) {
        if ($entry.Key -match '\.(md|json)$') {
            # Thunderstore requires UTF-8 metadata without a BOM, including the changelog.
            $bytes = [IO.File]::ReadAllBytes($entry.Value)
            if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
                $bytes = [byte[]]$bytes[3..($bytes.Length - 1)]
            }
            [void][Text.UTF8Encoding]::new($false, $true).GetString($bytes)
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
    foreach ($name in $files.Keys) {
        if ($entries -cnotcontains $name) { throw "Missing ZIP entry: $name" }
    }
    $entries | ForEach-Object { Write-Output "ZIP: $_" }
} finally { $check.Dispose() }
Get-Item -LiteralPath $zipPath | Select-Object FullName, Length
Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
