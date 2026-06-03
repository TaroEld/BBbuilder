param(
    [Parameter(Mandatory=$true)][string]$Source,
    [Parameter(Mandatory=$true)][string]$Destination
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$src = (Resolve-Path $Source).Path.TrimEnd('\','/')
$dest = [System.IO.Path]::GetFullPath($Destination)

$destDir = Split-Path $dest -Parent
if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir | Out-Null }
if (Test-Path $dest) { Remove-Item $dest -Force }

$zip = [System.IO.Compression.ZipFile]::Open($dest, 'Create')
try {
    Get-ChildItem -Path $src -Recurse -File |
        Where-Object { $_.Extension -ne '.pdb' } |
        ForEach-Object {
            $rel = $_.FullName.Substring($src.Length + 1).Replace('\','/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $rel) | Out-Null
        }
}
finally {
    $zip.Dispose()
}

Write-Host "Wrote $dest"
