Write-Host "AutoTileMapTool Plugin Push Utility" -ForegroundColor Green
Write-Host "Preparing to push plugin to GitHub..." -ForegroundColor Yellow

# Show current git status
git status

# Ask for confirmation
$CONTINUE = Read-Host "Continue with push? (Y/N)"

if ($CONTINUE -eq "Y" -or $CONTINUE -eq "y") {
    Write-Host "Pushing plugin to repository..." -ForegroundColor Yellow
    git subtree push --prefix Assets/AutoTileMapTool plugin-repo main
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Push successful! Plugin has been updated on GitHub." -ForegroundColor Green
    } 
    else {
        Write-Host "Error during push. Please check the log above." -ForegroundColor Red
    }
}
else {
    Write-Host "Operation cancelled." -ForegroundColor Yellow
}

Write-Host "Script completed. Press Enter to continue..."
Read-Host