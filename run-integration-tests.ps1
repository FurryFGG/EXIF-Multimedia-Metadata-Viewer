param(
    [string]$SampleDirectory
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$localSdk = Join-Path $repoRoot ".tools\dotnet\dotnet.exe"
if (-not (Test-Path -LiteralPath $localSdk)) {
    $localSdk = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "..\.tools\dotnet\dotnet.exe"))
}
if (Test-Path -LiteralPath $localSdk) {
    $dotnetPath = $localSdk
} else {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnetCommand) {
        throw ".NET 10 SDK is required to run integration tests."
    }
    $dotnetPath = $dotnetCommand.Source
}

$testProject = Join-Path $repoRoot "tests\ExifMetadata.IntegrationTests\ExifMetadata.IntegrationTests.csproj"
$arguments = @("run", "--project", $testProject, "-c", "Release", "--", $repoRoot)
if (-not [string]::IsNullOrWhiteSpace($SampleDirectory)) {
    $arguments += [System.IO.Path]::GetFullPath($SampleDirectory)
}
& $dotnetPath @arguments
exit $LASTEXITCODE
