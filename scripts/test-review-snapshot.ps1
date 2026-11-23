# Local test of the Google review snapshot job (Cute) against the Contentful *sandbox* environment.
# Test entry DataLocation.Test.GoogleReviewSnapshot covers locations 98, 5389 and 6106 only.
#
#   .\scripts\test-review-snapshot.ps1            # build, unit tests, dry run (nothing written)
#   .\scripts\test-review-snapshot.ps1 -Write     # ...then write and publish the 3 test locations in sandbox
#
# Needs: a Cute profile logged in to the space (cute login), Azure OpenAI with the gpt-5.4 deployment,
# and the Uberall key, either in .env as Cute__UberallApiKey=... or passed with -UberallApiKey.

param(
    [switch]$Write,
    [string]$UberallApiKey
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

if ($UberallApiKey) { $env:Cute__UberallApiKey = $UberallApiKey }
if (-not $env:Cute__UberallApiKey -and -not (Select-String -Path .env -Pattern '^Cute__UberallApiKey=' -Quiet -ErrorAction SilentlyContinue)) {
    throw "Set the Uberall key: -UberallApiKey <key>, or Cute__UberallApiKey=<key> in .env"
}

Write-Host "`n== Build ==" -ForegroundColor Cyan
dotnet build sln.sln -c Debug
if ($LASTEXITCODE) { throw "Build failed" }

Write-Host "`n== Unit tests (review snapshot) ==" -ForegroundColor Cyan
dotnet test tests/Cute.Unit.Tests --no-build --filter "FullyQualifiedName~CuteFunctionsReviewsTests"
if ($LASTEXITCODE) { throw "Tests failed" }

$cute = @('run', '--project', 'source/Cute', '--no-build', '--')
$key  = 'DataLocation.Test.GoogleReviewSnapshot'

Write-Host "`n== Dry run: prompts and model replies, nothing written ==" -ForegroundColor Cyan
dotnet @cute content generate-test -k $key -e sandbox
if ($LASTEXITCODE) { throw "Dry run failed" }

if (-not $Write) {
    Write-Host "`nDry run only. Re-run with -Write to write and publish the 3 test locations in sandbox." -ForegroundColor Yellow
    return
}

Write-Host "`n== Write and publish (sandbox) ==" -ForegroundColor Cyan
dotnet @cute content generate -k $key -o GenerateSingle -e sandbox --force
if ($LASTEXITCODE) { throw "Generate failed" }

Write-Host "`nDone. Check googleBusinessReviewSnapshot on 98, 5389 and 6106 in the sandbox environment." -ForegroundColor Green
