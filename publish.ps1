param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [switch]$FromVS
)

# --- Configuration ---
$ProjectFile = "SMSSearch/SMSSearch.csproj"
$PublishDir  = "./Publish"
$ZipPath     = "$PublishDir/SMS_Search.zip"
$ExeName     = "SMS Search.exe" 
$ErrorActionPreference = "Stop"
$hasErrors   = $false

Set-Location $PSScriptRoot

# --- Helper Functions ---
function Get-ProjectVersion {
    if (Test-Path $ProjectFile) {
        [xml]$csproj = Get-Content $ProjectFile
        $version = $csproj.Project.PropertyGroup.AssemblyVersion | Select-Object -First 1
        return $version.Replace("*", "0") 
    }
    return "0.0.0.0"
}

# --- 1. Build Logic ---
try {
    if (-not $FromVS) {
        Write-Host "--- SMS Search: $Configuration Build ---" -ForegroundColor Cyan
        dotnet clean $ProjectFile -c $Configuration -p:IsPublishing=true
        if ($LASTEXITCODE -ne 0) { throw "dotnet clean failed with exit code $LASTEXITCODE" }

        dotnet build $ProjectFile -c $Configuration -p:IsPublishing=true
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }

        Write-Host "Build Successful." -ForegroundColor Green
    }

    # --- 2. Git Workflow (Release Only) ---
    if ($Configuration -eq "Release") {
        if ((Read-Host "Create Version Bump PR? [Y/N]") -eq 'y') {
            $currentVersion = Get-ProjectVersion
            $branch = "chore/version-$currentVersion"
            
            git checkout -b $branch
            git add $ProjectFile
            git commit -m "Build: Version $currentVersion"
            git push -u origin $branch
            gh pr create --title "Release v$currentVersion" --body "Automated build sync."
            gh pr merge --merge --delete-branch
            git checkout main
            git pull origin main
        }
    }

    # --- 3. Publishing (Release Only) ---
    if ($Configuration -eq "Release") {
        if ((Read-Host "Create Single File Bundle for Testing? [Y/N]") -eq 'y') {
            Write-Host "Publishing to $PublishDir..." -ForegroundColor Yellow
            dotnet publish $ProjectFile -c $Configuration -r win-x64 --self-contained `
                -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IsPublishing=true -o $PublishDir
            if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
            
            Write-Host "Publish complete. Opening folder for testing..." -ForegroundColor Green
            Invoke-Item $PublishDir
            
            # --- 4. GitHub Release (Delayed Step) ---
            Write-Host "`n--- Deployment Step ---" -ForegroundColor Cyan
            $upload = Read-Host "Testing finished? Upload to GitHub Release now? [Y/N]"
            if ($upload -eq 'y') {
                if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
                Compress-Archive -Path "$PublishDir/$ExeName" -DestinationPath $ZipPath
                
                $finalVersion = Get-ProjectVersion
                Write-Host "Uploading v$finalVersion to GitHub..."
                gh release create "v$finalVersion" $ZipPath --title "v$finalVersion" --generate-notes
                if ($LASTEXITCODE -ne 0) { throw "gh release create failed with exit code $LASTEXITCODE" }
                Write-Host "Release Live!" -ForegroundColor Green
            }
        }
    }
    else {
        Write-Host "Debug build complete. Skipping Git and Publish steps." -ForegroundColor Gray
    }
}
catch {
    $hasErrors = $true
    Write-Host "`n[ERROR] $($_.Exception.Message)" -ForegroundColor Red
}

if ($hasErrors) {
    Read-Host "Press Enter to exit..."
}