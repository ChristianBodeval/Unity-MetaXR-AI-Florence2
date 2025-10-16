$ErrorActionPreference = "Stop"

$umlConfig = "Assets/Docs/UML/uml-config.json"
$addHeaderScript = "Assets/Docs/UML/add-hide-header.ps1"
$plantUMLJar = "Assets/Docs/UML/plantuml.jar"

Write-Host "Applying layout and visibility settings..."
powershell.exe -ExecutionPolicy Bypass -File $addHeaderScript -ConfigPath $umlConfig

if ($LASTEXITCODE -ne 0) {
    Write-Error "Header injection failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

$config = Get-Content -Raw -Path $umlConfig | ConvertFrom-Json
$umlInput = $config.folders.umlInput
$umlOutput = $config.folders.umlOutput

Write-Host "Generating UML diagrams..."
if (Test-Path $plantUMLJar) {
    Get-ChildItem -Path $umlInput -Filter *.puml | ForEach-Object {
        $inputPath = $_.FullName
        Write-Host "Rendering: $inputPath"
        java -jar $plantUMLJar -tpng $inputPath -o $umlOutput
    }
} else {
    Write-Host "PlantUML JAR not found at $plantUMLJar. Skipping render."
}

Write-Host "UML generation completed."
