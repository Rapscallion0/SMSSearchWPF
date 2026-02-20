param(
    [string]$Configuration = "Release",
    [switch]$FromVS
)

Set-Location $PSScriptRoot

if (-not $FromVS) {
    Write-Host "Building SMS Search ($Configuration)..."
    Write-Host ""

    Write-Host "Cleaning previous builds..."
    dotnet clean SMSSearch/SMSSearch.csproj -c $Configuration -p:IsPublishing=true
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Clean failed!" -ForegroundColor Red
        Read-Host "Press Enter to exit..."
        exit $LASTEXITCODE
    }

    Write-Host ""
    Write-Host "Building (Standard $Configuration)..."
    # This creates the framework-dependent build in bin/$Configuration/net10.0-windows/
    dotnet build SMSSearch/SMSSearch.csproj -c $Configuration -p:IsPublishing=true
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        Read-Host "Press Enter to exit..."
        exit $LASTEXITCODE
    }

    Write-Host "Standard Build Successful." -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "Build triggered from Visual Studio ($Configuration)."
    Write-Host "Skipping clean and standard build steps."
    Write-Host ""
}

$response = Read-Host "Do you want to create a Single File Bundle (Publish)? [Y/N]"
if ($response -eq 'Y' -or $response -eq 'y') {
    Write-Host ""
    Write-Host "Publishing Single File Bundle ($Configuration)..."
    Write-Host "This may take a moment."

    # Explicitly using flags to ensure Single File output, regardless of some local defaults.
    # The .csproj now includes <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
    # and <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    dotnet publish SMSSearch/SMSSearch.csproj -c $Configuration -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IsPublishing=true -o ./Publish

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Publish failed!" -ForegroundColor Red
        Read-Host "Press Enter to exit..."
        exit $LASTEXITCODE
    }

    Write-Host ""
    Write-Host "Publish Successful!" -ForegroundColor Green
    Write-Host "Executable is located in the 'Publish' folder."
    Write-Host ""
    Invoke-Item "Publish"
} else {
    Write-Host "Skipping publish step. Standard build artifacts are in bin/$Configuration/net10.0-windows/"
}

Read-Host "Press Enter to exit..."
