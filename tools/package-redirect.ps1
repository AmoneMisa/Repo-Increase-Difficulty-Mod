param(
    [string]$OutputDirectory
)

# Builds the final IncreaseDifficultyMod release: a pointer package with no plugin that depends on the renamed
# DeepRunOverhaul package (same version as manifest.json), so mod managers move players over on their next update.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'bin\thunderstore' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$main = Get-Content -LiteralPath (Join-Path $repo 'manifest.json') -Raw | ConvertFrom-Json
if ($main.name -cne 'DeepRunOverhaul') { throw 'manifest.json must be the DeepRunOverhaul package.' }
$version = $main.version_number
$manifest = [ordered]@{
    name = 'IncreaseDifficultyMod'
    version_number = $version
    website_url = $main.website_url
    description = 'Moved to DeepRunOverhaul (same mod, new name). Updating installs it for you; settings, perks and costumes carry over. You can then remove this package.'
    dependencies = @("WhitesLove-DeepRunOverhaul-$version")
}
if ($manifest.description.Length -gt 250) { throw 'Description exceeds 250 characters.' }

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$zipPath = Join-Path $OutputDirectory "IncreaseDifficultyMod-$version.zip"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$utf8 = [Text.UTF8Encoding]::new($false, $true)
$readme = [IO.File]::ReadAllBytes((Join-Path $repo 'packaging\redirect\README.md'))
$entries = [ordered]@{
    'manifest.json' = $utf8.GetBytes(($manifest | ConvertTo-Json -Depth 3))
    'README.md' = $readme
    'CHANGELOG.md' = $utf8.GetBytes("# $version`n`nIncreaseDifficultyMod is now DeepRunOverhaul. This release only installs WhitesLove-DeepRunOverhaul-$version.`n")
}
$stream = [IO.File]::Open($zipPath, [IO.FileMode]::Create, [IO.FileAccess]::ReadWrite)
$zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($entry in $entries.GetEnumerator()) {
        [void]$utf8.GetString($entry.Value) # metadata must be valid UTF-8 without a BOM
        $zipEntry = $zip.CreateEntry($entry.Key, [IO.Compression.CompressionLevel]::Optimal)
        $s = $zipEntry.Open(); try { $s.Write($entry.Value, 0, $entry.Value.Length) } finally { $s.Dispose() }
    }
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $repo 'icon.png'), 'icon.png',
        [IO.Compression.CompressionLevel]::Optimal) | Out-Null
} finally { $zip.Dispose(); $stream.Dispose() }
$check = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $names = @($check.Entries | ForEach-Object FullName)
    foreach ($name in @('manifest.json', 'README.md', 'CHANGELOG.md', 'icon.png')) { if ($names -cnotcontains $name) { throw "Missing ZIP entry: $name" } }
    if ($names | Where-Object { $_ -like 'plugins/*' -or $_ -like '*.dll' }) { throw 'The pointer package must not contain a plugin.' }
    $names | ForEach-Object { Write-Output "ZIP: $_" }
} finally { $check.Dispose() }
Get-Item -LiteralPath $zipPath | Select-Object FullName, Length
