param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("None", "Major", "Minor", "Patch", "Build")]
    [string]$Bump = "None",
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
function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Description,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )
    $global:LASTEXITCODE = 0
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Confirm-Yes {
    param(
        [Parameter(Mandatory = $true)][string]$Prompt,
        [bool]$DefaultYes = $false
    )
    $suffix = if ($DefaultYes) { "[Y/n]" } else { "[y/N]" }
    $response = (Read-Host "$Prompt $suffix").Trim()
    if ([string]::IsNullOrEmpty($response)) {
        return $DefaultYes
    }
    return $response -match '^[yY](es)?$'
}

function Stop-RunningApp {
    $processes = Get-Process -Name "SMS Search" -ErrorAction SilentlyContinue
    if ($processes) {
        Write-Host "Found running instances of SMS Search. Terminating..." -ForegroundColor Yellow
        $processes | Stop-Process -Force
        Start-Sleep -Seconds 1
    }
}

function Get-ProjectVersion {
    if (Test-Path $ProjectFile) {
        [xml]$csproj = Get-Content $ProjectFile
        $version = $csproj.Project.PropertyGroup.AssemblyVersion | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($version)) {
            return $version.Replace("*", "0")
        }
    }
    return "0.0.0.0"
}

function Set-ProjectVersion {
    param([Parameter(Mandatory = $true)][string]$NewVersion)

    $content = Get-Content $ProjectFile -Raw
    $updated = $content `
        -replace '<AssemblyVersion>[^<]*</AssemblyVersion>', "<AssemblyVersion>$NewVersion</AssemblyVersion>" `
        -replace '<FileVersion>[^<]*</FileVersion>', "<FileVersion>$NewVersion</FileVersion>"

    if ($updated -eq $content) {
        Write-Host "Version in $ProjectFile is already $NewVersion." -ForegroundColor Gray
        return
    }

    Set-Content -Path $ProjectFile -Value $updated -NoNewline
    Write-Host "Updated $ProjectFile AssemblyVersion & FileVersion to $NewVersion." -ForegroundColor Green
}

function Get-SuggestedBump {
    $lastTag = git describe --tags --abbrev=0 2>$null
    $range = if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($lastTag)) {
        "$lastTag..HEAD"
    } else {
        "HEAD"
    }

    $commits = git log $range --format="%s%n%b" 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $commits) {
        return "Patch"
    }

    $logText = ($commits -join "`n")
    if ($logText -match '(?m)(^BREAKING CHANGE:|^[a-z]+(\([^)]+\))?!:)') {
        return "Major"
    }
    if ($logText -match '(?m)^feat(\([^)]+\))?:') {
        return "Minor"
    }
    return "Patch"
}

function Resolve-NextVersion {
    param(
        [Parameter(Mandatory = $true)][string]$CurrentVersion,
        [Parameter(Mandatory = $true)][string]$RequestedBump
    )

    $parts = $CurrentVersion.Split('.')
    $major = if ($parts.Length -ge 1) { [int]$parts[0] } else { 1 }
    $minor = if ($parts.Length -ge 2) { [int]$parts[1] } else { 0 }
    $patch = if ($parts.Length -ge 3) { [int]$parts[2] } else { 0 }

    $now = Get-Date
    $yyddd = [int]("{0}{1:D3}" -f $now.ToString("yy"), $now.DayOfYear)

    $bumpType = $RequestedBump
    if ($bumpType -eq "None") {
        $suggested = Get-SuggestedBump
        Write-Host "`nCurrent version: $CurrentVersion (Suggested bump from commits: $suggested)" -ForegroundColor Cyan
        $choice = (Read-Host "Select bump: [M]ajor / [m]inor / [p]atch / [b]uild / [k]eep (Enter = $suggested)").Trim()

        if ([string]::IsNullOrEmpty($choice)) {
            $bumpType = $suggested
        }
        else {
            switch -CaseSensitive ($choice) {
                'M' { $bumpType = "Major" }
                'm' { $bumpType = "Minor" }
                'p' { $bumpType = "Patch" }
                'P' { $bumpType = "Patch" }
                'b' { $bumpType = "Build" }
                'B' { $bumpType = "Build" }
                'k' { $bumpType = "None" }
                'K' { $bumpType = "None" }
                default { throw "Invalid version bump selection '$choice'." }
            }
        }
    }

    switch ($bumpType) {
        "Major" { return "$($major + 1).0.0.$yyddd" }
        "Minor" { return "$major.$($minor + 1).0.$yyddd" }
        "Patch" { return "$major.$minor.$($patch + 1).$yyddd" }
        "Build" { return "$major.$minor.$patch.$yyddd" }
        default { return $CurrentVersion }
    }
}

function Sync-BranchToMain {
    $dirty = git status --porcelain
    if ($LASTEXITCODE -ne 0) { throw "Failed to check git status." }

    $currentBranch = (git branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($currentBranch)) {
        throw "Could not determine active Git branch."
    }

    if ($dirty) {
        Write-Host "`nUncommitted changes detected on '$currentBranch':" -ForegroundColor Yellow
        $dirty | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
        if (Confirm-Yes "Stage and commit all changes to '$currentBranch' before continuing?") {
            $commitMsg = Read-Host "Commit message"
            if ([string]::IsNullOrWhiteSpace($commitMsg)) {
                throw "Commit message cannot be empty."
            }
            Invoke-Step "git add" { git add -A }
            Invoke-Step "git commit" { git commit -m $commitMsg }
        }
        else {
            throw "Working tree must be clean before merging or switching branches."
        }
    }

    if ($currentBranch -ne "main") {
        Write-Host "`nCurrently on branch '$currentBranch'." -ForegroundColor Cyan
        if (Confirm-Yes "Push '$currentBranch' and merge PR into 'main'?" -DefaultYes $true) {
            Invoke-Step "git push" { git push -u origin $currentBranch }
            $existingPr = gh pr list --head $currentBranch --state open --json number --jq ".[0].number" 2>$null
            if ([string]::IsNullOrWhiteSpace($existingPr)) {
                Invoke-Step "gh pr create" { gh pr create --base main --head $currentBranch --fill }
            }
            Invoke-Step "gh pr merge" { gh pr merge $currentBranch --merge --delete-branch }
            Invoke-Step "git checkout main" { git checkout main }
            Invoke-Step "git pull origin main" { git pull origin main --ff-only }
        }
        else {
            throw "Release workflow requires merging '$currentBranch' into 'main' first."
        }
    }
    else {
        Invoke-Step "git pull origin main" { git pull origin main --ff-only }
    }
}

# --- Main Execution ---
try {
    Stop-RunningApp

    if ($FromVS) {
        Write-Host "Invoked from Visual Studio PostBuild ($Configuration). Skipping interactive Git/Publish steps." -ForegroundColor Gray
        exit 0
    }

    if ($Configuration -eq "Release") {
        # --- 1. Git Branch Merge & Version Bump ---
        $doGitWorkflow = Confirm-Yes "Run Git merge & Version Bump PR workflow?"
        if ($doGitWorkflow) {
            Invoke-Step "gh auth status" { gh auth status }
            Sync-BranchToMain

            $currentVersion = Get-ProjectVersion
            $nextVersion = Resolve-NextVersion -CurrentVersion $currentVersion -RequestedBump $Bump

            $existingTag = git tag -l "v$nextVersion"
            if (-not [string]::IsNullOrWhiteSpace($existingTag)) {
                throw "Tag 'v$nextVersion' already exists. Choose a higher version bump."
            }

            if ($nextVersion -ne $currentVersion) {
                Set-ProjectVersion -NewVersion $nextVersion
            }
        }

        # --- 2. Build Logic ---
        Write-Host "--- SMS Search: $Configuration Build ---" -ForegroundColor Cyan
        Invoke-Step "dotnet clean" { dotnet clean $ProjectFile -c $Configuration -p:IsPublishing=true }
        Invoke-Step "dotnet build" { dotnet build $ProjectFile -c $Configuration -p:IsPublishing=true }
        Write-Host "Build Successful." -ForegroundColor Green

        # --- 3. Commit & Merge Version Bump PR ---
        if ($doGitWorkflow) {
            $projectDirty = git status --porcelain $ProjectFile
            if ($projectDirty) {
                $finalVersion = Get-ProjectVersion
                $branch = "chore/version-$finalVersion"

                Invoke-Step "git checkout -b $branch" { git checkout -b $branch }
                Invoke-Step "git add $ProjectFile" { git add $ProjectFile }
                Invoke-Step "git commit" { git commit -m "chore: bump version to $finalVersion" }
                Invoke-Step "git push" { git push -u origin $branch }
                Invoke-Step "gh pr create" { gh pr create --base main --head $branch --title "Release v$finalVersion" --body "Automated version bump and release build." }
                Invoke-Step "gh pr merge" { gh pr merge $branch --merge --delete-branch }
                Invoke-Step "git checkout main" { git checkout main }
                Invoke-Step "git pull origin main" { git pull origin main --ff-only }
            }
        }

        # --- 4. Publishing ---
        if (Confirm-Yes "Create Single File Bundle for Testing?") {
            Write-Host "Publishing to $PublishDir..." -ForegroundColor Yellow
            Invoke-Step "dotnet publish" {
                dotnet publish $ProjectFile -c $Configuration -r win-x64 --self-contained `
                    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IsPublishing=true -o $PublishDir
            }

            Write-Host "Publish complete. Opening folder for testing..." -ForegroundColor Green
            Invoke-Item $PublishDir

            # --- 5. GitHub Release ---
            Write-Host "`n--- Deployment Step ---" -ForegroundColor Cyan
            if (Confirm-Yes "Testing finished? Upload to GitHub Release now?") {
                if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
                Compress-Archive -Path "$PublishDir/$ExeName" -DestinationPath $ZipPath

                $finalVersion = Get-ProjectVersion
                Write-Host "Uploading v$finalVersion to GitHub..."
                Invoke-Step "gh release create" { gh release create "v$finalVersion" $ZipPath --title "v$finalVersion" --generate-notes }
                Write-Host "Release Live!" -ForegroundColor Green
            }
        }
    }
    else {
        Write-Host "--- SMS Search: $Configuration Build ---" -ForegroundColor Cyan
        Invoke-Step "dotnet clean" { dotnet clean $ProjectFile -c $Configuration -p:IsPublishing=true }
        Invoke-Step "dotnet build" { dotnet build $ProjectFile -c $Configuration -p:IsPublishing=true }
        Write-Host "Debug build complete. Skipping Git and Publish steps." -ForegroundColor Gray
    }
}
catch {
    $hasErrors = $true
    Write-Host "`n[ERROR] $($_.Exception.Message)" -ForegroundColor Red
}

if ($hasErrors) {
    Read-Host "Press Enter to exit..."
    exit 1
}