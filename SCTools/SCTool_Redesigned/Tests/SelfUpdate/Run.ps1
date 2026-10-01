param(
    [string]$ArtifactsPath,
    [switch]$SkipBuild,
    [switch]$UnitOnly
)

$ErrorActionPreference = 'Stop'
if (!$ArtifactsPath) {
    $appDirectory = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $ArtifactsPath = Join-Path $appDirectory ('bin\SelfUpdateTests\' + [guid]::NewGuid().ToString('N'))
}
New-Item -ItemType Directory -Force -Path $ArtifactsPath | Out-Null
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $ArtifactsPath 'bundle-extract'
$env:DOTNET_DISABLE_GUI_ERRORS = '1'

if (!$SkipBuild) {
    $fixture = Join-Path $PSScriptRoot 'Fixture\Fixture.csproj'
    & dotnet build $fixture -c Release -p:FixtureVersion=1.4.1.2 -p:SelfContained=false -p:PublishSingleFile=false -o (Join-Path $ArtifactsPath 'fixtures\old-multi')
    if ($LASTEXITCODE) { throw 'Multi-file fixture build failed.' }
    foreach ($item in @(
        @{ Name = 'old-single'; FileVersion = '1.4.1.2'; AssemblyVersion = '1.4.1.2' },
        @{ Name = 'new-single'; FileVersion = '1.4.1.3'; AssemblyVersion = '1.4.1.3' },
        @{ Name = 'wrong-version'; FileVersion = '1.4.1.3'; AssemblyVersion = '1.4.1.4' }
    )) {
        & dotnet publish $fixture -c Release "-p:FixtureVersion=$($item.FileVersion)" "-p:FixtureAssemblyVersion=$($item.AssemblyVersion)" -p:SelfContained=true -p:PublishSingleFile=true -o (Join-Path $ArtifactsPath ('fixtures\' + $item.Name))
        if ($LASTEXITCODE) { throw ($item.Name + ' fixture publish failed.') }
    }
    & dotnet build (Join-Path $PSScriptRoot 'SelfUpdate.Tests.csproj') -c Release -r win-x64 -p:SelfContained=false -o (Join-Path $ArtifactsPath 'runner')
    if ($LASTEXITCODE) { throw 'Regression runner build failed.' }
}

$runnerArguments = @($ArtifactsPath)
if ($UnitOnly) { $runnerArguments += '--unit-only' }
& (Join-Path $ArtifactsPath 'runner\SelfUpdate.Tests.exe') @runnerArguments
exit $LASTEXITCODE
