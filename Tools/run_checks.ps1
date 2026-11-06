$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $ProjectRoot
try {
    dotnet run --project Tools/CoreHarness -- check --root .
    if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
} finally { Pop-Location }

