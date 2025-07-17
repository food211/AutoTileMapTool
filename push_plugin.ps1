Write-Host "AutoTileMapTool Plugin Push Utility" -ForegroundColor Green
Write-Host "Preparing to push plugin to GitHub..." -ForegroundColor Yellow

# Show current git status
git status

# 添加初始化选项
Write-Host "Options:" -ForegroundColor Cyan
Write-Host "0. Initialize subtree (first time setup)" -ForegroundColor Cyan
Write-Host "1. Pull remote changes first (recommended)" -ForegroundColor Cyan
Write-Host "2. Push directly (may fail if remote has changes)" -ForegroundColor Cyan
Write-Host "3. Compressed push (reduces history, faster)" -ForegroundColor Cyan
Write-Host "4. Direct snapshot push (fastest)" -ForegroundColor Cyan
Write-Host "5. Cancel operation" -ForegroundColor Cyan

$OPTION = Read-Host "Select option (0-5)"

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
elseif ($OPTION -eq "3") {
    # 压缩推送（使用split+merge方式）
    Write-Host "WARNING: Compressed push will create a new commit with current state only." -ForegroundColor Yellow
    Write-Host "Previous history in the plugin repo will be preserved but not connected to this push." -ForegroundColor Yellow
    $CONTINUE = Read-Host "Continue with compressed push? (Y/N)"
    
    if ($CONTINUE -eq "Y" -or $CONTINUE -eq "y") {
        # 获取当前分支
        $currentBranch = git rev-parse --abbrev-ref HEAD
        
        # 创建临时分支来存储当前子树内容
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $tempBranch = "temp_plugin_$timestamp"
        
        Write-Host "Creating temporary branch for plugin content..." -ForegroundColor Yellow
        git checkout -b $tempBranch
        
        # 删除除了插件目录外的所有内容
        Write-Host "Isolating plugin content..." -ForegroundColor Yellow
        Get-ChildItem -Path . -Exclude "Assets" | Remove-Item -Recurse -Force
        Get-ChildItem -Path "Assets" -Exclude "AutoTileMapTool" | Remove-Item -Recurse -Force
        
        # 移动插件内容到根目录
        Write-Host "Preparing content for push..." -ForegroundColor Yellow
        Move-Item -Path "Assets/AutoTileMapTool/*" -Destination "."
        Remove-Item -Path "Assets" -Recurse -Force
        
        # 添加所有更改并提交
        git add -A
        git commit -m "Compressed plugin update $(Get-Date -Format 'yyyy-MM-dd')"
        
        # 推送到插件仓库
        Write-Host "Pushing compressed content to plugin repository..." -ForegroundColor Yellow
        git push plugin-repo $tempBranch:main --force
        
        # 检查推送结果
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Compressed push successful! Plugin has been updated on GitHub." -ForegroundColor Green
        } 
        else {
            Write-Host "Error during compressed push. Please check the log above." -ForegroundColor Red
        }
        
        # 返回原始分支并删除临时分支
        Write-Host "Cleaning up..." -ForegroundColor Yellow
        git checkout $currentBranch
        git branch -D $tempBranch
    }
    else {
        Write-Host "Compressed push operation cancelled." -ForegroundColor Yellow
    }
}
elseif ($OPTION -eq "4") {
    # 直接快照推送（最快）
    Write-Host "WARNING: Direct snapshot push will replace the remote repository content." -ForegroundColor Yellow
    Write-Host "All remote history will be lost. This is the fastest method but most destructive." -ForegroundColor Yellow
    $CONTINUE = Read-Host "Continue with direct snapshot push? (Y/N)"
    
    if ($CONTINUE -eq "Y" -or $CONTINUE -eq "y") {
        # 创建临时目录
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $tempDir = "temp_plugin_$timestamp"
        
        Write-Host "Creating temporary directory for plugin content..." -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $tempDir | Out-Null
        
        # 复制插件内容到临时目录
        Write-Host "Copying plugin content..." -ForegroundColor Yellow
        Copy-Item -Path "Assets/AutoTileMapTool/*" -Destination $tempDir -Recurse
        
        # 初始化临时Git仓库
        Write-Host "Initializing temporary Git repository..." -ForegroundColor Yellow
        Push-Location $tempDir
        git init
        
        # 确保使用main作为默认分支
        git checkout -b main
        
        git add -A
        git commit -m "Snapshot update $(Get-Date -Format 'yyyy-MM-dd')"
        
        # 获取远程仓库URL
        Pop-Location
        $remoteUrl = git remote get-url plugin-repo
        
        # 推送到插件仓库
        Write-Host "Pushing snapshot to plugin repository..." -ForegroundColor Yellow
        Push-Location $tempDir
        git remote add origin $remoteUrl
        git push -f origin main
        
        # 检查推送结果
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Direct snapshot push successful! Plugin has been updated on GitHub." -ForegroundColor Green
        } 
        else {
            Write-Host "Error during direct snapshot push. Please check the log above." -ForegroundColor Red
        }
        
        # 清理临时目录
        Pop-Location
        Write-Host "Cleaning up..." -ForegroundColor Yellow
        Remove-Item -Path $tempDir -Recurse -Force
    }
    else {
        Write-Host "Direct snapshot push operation cancelled." -ForegroundColor Yellow
    }
}
else {
    Write-Host "Operation cancelled." -ForegroundColor Yellow
}

Write-Host "Script completed. Press Enter to continue..."
Read-Host