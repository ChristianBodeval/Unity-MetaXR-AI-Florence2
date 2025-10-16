param(
    [Parameter(Mandatory = $false)]
    [string] $ConfigPath = "Assets/Docs/UML/uml-config.json",
    [Parameter(Mandatory = $false)]
    [string] $UmlPath
)

if (!(Test-Path -LiteralPath $ConfigPath)) {
    Write-Host "Config file not found: $ConfigPath"
    exit 1
}

$config = Get-Content -Raw -Path $ConfigPath | ConvertFrom-Json

$layoutDirection = switch ($config.layout) {
    "left_to_right" { "left to right direction" }
    "right_to_left" { "right to left direction" }
    "bottom_to_top" { "bottom to top direction" }
    default { "top to bottom direction" }
}

$hideClasses = $config.hideList
$defaultVisibility = $config.defaultVisibility
$umlInput = $config.folders.umlInput
$umlOutput = $config.folders.umlOutput
$generationMode = $config.generationMode

if (!(Test-Path -LiteralPath $umlInput)) {
    Write-Host "Input folder not found: $umlInput"
    exit 1
}
if (!(Test-Path -LiteralPath $umlOutput)) {
    New-Item -ItemType Directory -Force -Path $umlOutput | Out-Null
}

function Apply-HideDirectives($umlFilePath) {
    Write-Host "Patching UML file: $umlFilePath"

    $lines = Get-Content -LiteralPath $umlFilePath
    $startIndex = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*@startuml\b') {
            $startIndex = $i
            break
        }
    }

    if ($startIndex -lt 0) {
        Write-Host "No @startuml found in $umlFilePath"
        return
    }

    $headerLines = @(
        $layoutDirection,
        ""
    )

    if ($defaultVisibility -eq "hidden") {
        foreach ($className in $hideClasses) {
            $headerLines += "hide $className"
        }
    } elseif ($defaultVisibility -eq "visible") {
        foreach ($className in $hideClasses) {
            $headerLines += "show $className"
        }
    }

    $headerLines += ""

    if (($lines -join "`n") -notmatch "hide " -and ($lines -join "`n") -notmatch "show ") {
        $before = $lines[0..$startIndex]
        $after = @()
        if ($startIndex + 1 -lt $lines.Count) {
            $after = $lines[($startIndex + 1)..($lines.Count - 1)]
        }

        $lines = @()
        $lines += $before
        $lines += $headerLines
        $lines += $after

        $seen = @{}
        $filtered = New-Object System.Collections.Generic.List[string]
        foreach ($line in $lines) {
            if ($line -match '^\s*(class|enum|struct)\s+([A-Za-z0-9_`]+)') {
                $typeName = $matches[2]
                if (-not $seen.ContainsKey($typeName)) {
                    $seen[$typeName] = $true
                    $filtered.Add($line)
                }
            } else {
                $filtered.Add($line)
            }
        }

        $filtered | Set-Content -LiteralPath $umlFilePath -Encoding UTF8
        Write-Host "Updated: $umlFilePath"
    } else {
        Write-Host "Skipped (already patched): $umlFilePath"
    }
}

switch ($generationMode) {
    "single" {
        $umlPath = Join-Path $umlInput "include.puml"
        if (Test-Path $umlPath) { Apply-HideDirectives $umlPath }
        else { Write-Host "No include.puml found in $umlInput" }
    }
    "each_in_list" {
        foreach ($fileName in $config.hideList) {
            $umlPath = Join-Path $umlInput "$fileName.puml"
            if (Test-Path $umlPath) { Apply-HideDirectives $umlPath }
        }
    }
    "each_in_folder" {
        Get-ChildItem -Path $umlInput -Filter *.puml | ForEach-Object {
            Apply-HideDirectives $_.FullName
        }
    }
    default {
        Write-Host "Unknown generationMode: $generationMode"
    }
}

Write-Host "UML patching complete. Output folder: $umlOutput"
