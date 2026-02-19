Write-Host "Building SMS Search Single File Executable..."
Write-Host ""

Write-Host "Cleaning previous builds..."
dotnet clean SMSSearch/SMSSearch.csproj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Clean failed!" -ForegroundColor Red
    Read-Host "Press Enter to exit..."
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Publishing..."
Write-Host "This may take a moment."
# The .csproj is now configured to bundle native libraries and content (IncludeNativeLibrariesForSelfExtract=true, IncludeAllContentForSelfExtract=true)
# We avoid overriding those with command line flags here.
dotnet publish SMSSearch/SMSSearch.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./Publish

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    Read-Host "Press Enter to exit..."
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Build Successful!" -ForegroundColor Green
Write-Host "Executable is located in the 'Publish' folder."
Write-Host ""
Invoke-Item "Publish"
Read-Host "Press Enter to exit..."
