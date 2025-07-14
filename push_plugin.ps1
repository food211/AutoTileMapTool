Write-Host "AutoTileMapTool Plugin Push Utility" -ForegroundColor Green
Write-Host "Preparing to push plugin to GitHub..." -ForegroundColor Yellow

# Show current git status
git status

# 添加初始化选项
Write-Host "Options:" -ForegroundColor Cyan
Write-Host "0. Initialize subtree (first time setup)" -ForegroundColor Cyan
Write-Host "1. Pull remote changes first (recommended)" -ForegroundColor Cyan
Write-Host "2. Push directly (may fail if remote has changes)" -ForegroundColor Cyan
Write-Host "3. Cancel operation" -ForegroundColor Cyan

$OPTION = Read-Host "Select option (0-3)"

if ($OPTION -eq "0") {
    # 初始化 subtree 关系
    Write-Host "Initializing subtree relationship..." -ForegroundColor Yellow
    git subtree add --prefix=Assets/AutoTileMapTool plugin-repo main --squash
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Subtree initialized successfully!" -ForegroundColor Green
        
        # 询问是否继续推送
        $CONTINUE = Read-Host "Do you want to push your changes now? (Y/N)"
        
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
    } 
    else {
        Write-Host "Failed to initialize subtree. Please check the log above." -ForegroundColor Red
        Write-Host "You might need to manually resolve conflicts or use --allow-unrelated-histories option." -ForegroundColor Yellow
        
        # 提供高级选项
        $ADVANCED = Read-Host "Try with --allow-unrelated-histories? (Y/N)"
        
        if ($ADVANCED -eq "Y" -or $ADVANCED -eq "y") {
            git subtree add --prefix=Assets/AutoTileMapTool plugin-repo main --squash --allow-unrelated-histories
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Subtree initialized successfully with unrelated histories!" -ForegroundColor Green
            } 
            else {
                Write-Host "Still failed to initialize subtree. You might need manual intervention." -ForegroundColor Red
            }
        }
    }
}
elseif ($OPTION -eq "1") {
    # 先拉取远程仓库的更改
    Write-Host "Pulling remote changes from plugin repository..." -ForegroundColor Yellow
    git fetch plugin-repo main
    
    # 尝试合并远程更改到本地子树
    Write-Host "Merging remote changes into local subtree..." -ForegroundColor Yellow
    git subtree pull --prefix Assets/AutoTileMapTool plugin-repo main --squash
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Merge conflicts detected. Please resolve conflicts manually and then run the script again." -ForegroundColor Red
        Write-Host "Script completed. Press Enter to continue..."
        Read-Host
        exit
    }
    
    # 拉取成功后，询问是否继续推送
    $CONTINUE = Read-Host "Remote changes pulled successfully. Continue with push? (Y/N)"
    
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
        Write-Host "Push operation cancelled." -ForegroundColor Yellow
    }
}
elseif ($OPTION -eq "2") {
    # 直接推送（可能会失败）
    $CONTINUE = Read-Host "Continue with direct push? This may fail if remote has changes. (Y/N)"
    
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
}
else {
    Write-Host "Operation cancelled." -ForegroundColor Yellow
}

Write-Host "Script completed. Press Enter to continue..."
Read-Host