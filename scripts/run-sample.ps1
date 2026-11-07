# Opens the desktop app on a disposable copy of samples/runner-game. Each run restores the
# sample files, and LOCALIZER_WORKSPACE keeps the sample's project binding apart from Storage/.
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$workspace = Join-Path $repoRoot 'Storage/sample-run'
$projectFolder = Join-Path $workspace 'project'

New-Item -ItemType Directory -Force -Path $workspace | Out-Null
Copy-Item -Path (Join-Path $repoRoot 'samples/runner-game/*') -Destination $workspace -Recurse -Force

$settings = @{
    bindings = @(
        @{
            catalogId      = 'runner-game'
            folderPath     = $projectFolder
            importFileName = 'UI.csv'
            exportFileName = 'UI.export.csv'
        }
    )
} | ConvertTo-Json -Depth 3

# The settings store rejects a byte order mark, which Windows PowerShell's file cmdlets would add.
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $workspace 'project-folders.json'), $settings, $utf8NoBom)

$previousWorkspace = $env:LOCALIZER_WORKSPACE
$env:LOCALIZER_WORKSPACE = $workspace
try {
    dotnet run --project (Join-Path $repoRoot 'Localizer.Desktop') -- (Join-Path $workspace 'runner-game.json')
}
finally {
    $env:LOCALIZER_WORKSPACE = $previousWorkspace
}
