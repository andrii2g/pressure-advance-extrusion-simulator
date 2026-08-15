$ErrorActionPreference = 'Stop'

$solution = Get-ChildItem -File | Where-Object { $_.Extension -in '.sln', '.slnx' } | Select-Object -First 1
if (-not $solution) { throw 'No solution file found.' }

dotnet restore $solution.FullName
dotnet build $solution.FullName -c Release --no-restore
dotnet test $solution.FullName -c Release --no-build

dotnet run -c Release --project src/PressureAdvance.Cli -- compare --scenario corner --k 0.04 --output artifacts/verify-compare
dotnet run -c Release --project src/PressureAdvance.Cli -- sweep --scenario corner --k-start 0 --k-end 0.10 --k-step 0.005 --output artifacts/verify-sweep

$requiredCompare = @(
    'artifacts/verify-compare/comparison.svg'
)

$requiredSweep = @(
    'artifacts/verify-sweep/k-sweep.svg',
    'artifacts/verify-sweep/k-sweep.csv',
    'artifacts/verify-sweep/k-sweep.json'
)

foreach ($file in ($requiredCompare + $requiredSweep)) {
    if (-not (Test-Path $file)) { throw "Missing expected artifact: $file" }
}

Write-Host 'Verification completed successfully.'
