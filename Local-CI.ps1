$ErrorActionPreference = 'Stop'

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " FRACTAL-BLT // LOCAL CI AUTOMATION                       " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/3] Restoring Dependencies..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) { throw "Restore failed" }

Write-Host ""
Write-Host "[2/3] Running Unit Tests..." -ForegroundColor Yellow
dotnet test --no-restore --verbosity normal
if ($LASTEXITCODE -ne 0) { throw "Tests failed" }

Write-Host ""
Write-Host "[3/3] Compiling NativeAOT Release Binaries..." -ForegroundColor Yellow
dotnet publish FractalServe/FractalServe.csproj -c Release -r win-x64 /p:PublishAot=true
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host " SUCCESS: All Local CI Checks Passed!                   " -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
