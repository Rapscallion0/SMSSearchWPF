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
if ($Configuration -eq "Release") {
    $bumpResponse = Read-Host "Do you want to create a Git Version Bump? [Y/N]"
    if ($bumpResponse -eq 'Y' -or $bumpResponse -eq 'y') {
        Write-Host ""
        Write-Host "Creating Git Version Bump PR..."

        $version = "Unknown"
        if (Test-Path "SMSSearch/SMSSearch.csproj") {
            [xml]$csproj = Get-Content "SMSSearch/SMSSearch.csproj"
            $versionNode = $csproj.Project.PropertyGroup | Where-Object { $_.AssemblyVersion }
            if ($versionNode) {
                $version = $versionNode.AssemblyVersion
            }
        }

        if ($version -ne "Unknown") {
            $branchName = "chore/bump-version-$version"
            $commitMessage = "Bump version to $version"

            # Switch to new branch
            git checkout -b $branchName

            # Stage only .csproj
            git add SMSSearch/SMSSearch.csproj

            # Commit changes
            git commit -m $commitMessage

            # Push branch
            git push -u origin $branchName

            # Create PR
            gh pr create --title $commitMessage --body "Automated version bump"

            # Merge PR
            gh pr merge --merge --delete-branch

            # Switch back to main branch
            git checkout main

            # Pull latest changes
            git pull origin main

            Write-Host "Version bump PR created and merged successfully!" -ForegroundColor Green
        } else {
            Write-Host "Failed to read version from SMSSearch.csproj. Skipping version bump." -ForegroundColor Red
        }
        Write-Host ""
    }
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

    $uploadResponse = Read-Host "Do you want to upload SMS Search.exe (zipped) to GitHub? [Y/N]"
    if ($uploadResponse -eq 'Y' -or $uploadResponse -eq 'y') {
        Write-Host "Zipping Executable..."
        $zipPath = ".\Publish\SMS_Search.zip"
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        Compress-Archive -Path ".\Publish\SMS Search.exe" -DestinationPath $zipPath

        $version = "Unknown"
        if (Test-Path "SMSSearch/SMSSearch.csproj") {
            [xml]$csproj = Get-Content "SMSSearch/SMSSearch.csproj"
            $versionNode = $csproj.Project.PropertyGroup | Where-Object { $_.AssemblyVersion }
            if ($versionNode) {
                $version = $versionNode.AssemblyVersion
            }
        }

        Write-Host "Uploading v$version to GitHub..."
        gh release create "v$version" $zipPath --title "v$version" --generate-notes
        if ($LASTEXITCODE -ne 0) {
            Write-Host "GitHub upload failed!" -ForegroundColor Red
        } else {
            Write-Host "GitHub upload successful!" -ForegroundColor Green
        }
        Write-Host ""
    }

    Invoke-Item "Publish"
} else {
    Write-Host "Skipping publish step. Standard build artifacts are in bin/$Configuration/net10.0-windows/"
}

Read-Host "Press Enter to exit..."
