@echo off
echo Building SMS Search Single File Executable...
echo.

echo Cleaning previous builds...
dotnet clean SMSSearch/SMSSearch.csproj -c Release
if %errorlevel% neq 0 (
    echo Clean failed!
    pause
    exit /b %errorlevel%
)

echo.
echo Publishing...
echo This may take a moment.
dotnet publish SMSSearch/SMSSearch.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -o ./Publish

if %errorlevel% neq 0 (
    echo Publish failed!
    pause
    exit /b %errorlevel%
)

echo.
echo Build Successful!
echo Executable is located in the 'Publish' folder.
echo.
start "" "Publish"
pause
