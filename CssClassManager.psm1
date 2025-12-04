<#
.MODULE_NAME
    CssClassManager

.SYNOPSIS
    CSS 自動化管理與重構工具集 (Agentic CSS Refactoring Toolkit)。
    提供 CSS 解析、實體化管理 (JSON)、屬性操作與檔案直接修改功能。

.DESCRIPTION
    此模組專為自動化 CSS 維護任務設計，具備以下核心能力：
    1. **解析核心**: 使用狀態機解析 CSS，支援嵌套 (Media Query)、複雜字串與註解。
    2. **實體化系統**: 將 CSS Class 轉為原子化的 JSON 實體，支援版本控制與精細修改。
    3. **直接操作**: 支援直接對 CSS 檔案進行屬性增刪改與合併，無需中間轉換。
    4. **安全機制**: 所有破壞性操作 (Remove/Replace) 皆自動建立備份，並驗證語法完整性。

.FUNCTIONS
    [核心解析]
    - Get-CssClasses: 解析 CSS 檔案，回傳結構化物件 (含 StartIndex, BlockEnd)。
    - Compare-CssStyle: 語義化比較兩個 CSS 樣式區塊 (忽略空白與順序)。
    - Remove-CssClass: 安全移除指定的 Class (支援群組選擇器防護)。

    [實體化管理 (JSON)]
    - Export-CssToEntities: 將 CSS 檔案拆解為多個 JSON 實體檔案。
    - Import-CssFromEntities: 將 JSON 實體檔案組合成 CSS 檔案。
    - Get-CssEntity: 讀取 JSON 實體為可操作物件。
    - Update-CssEntityProperty: 修改 JSON 實體的屬性 (Set/Remove)。
    - Merge-CssEntity: 合併兩個 JSON 實體 (支援 Overwrite/FillMissing/PruneDuplicate)。

    [檔案直接操作 (Direct)]
    - Update-CssClassProperty: 直接修改 CSS 檔案中的 Class 屬性。
    - Merge-CssClass: 將外部定義合併到 CSS 檔案中的 Class。

    [格式轉換]
    - ConvertTo-CssJson: CSS 物件 -> JSON 結構 (屬性自動排序)。
    - ConvertFrom-CssJson: JSON 結構 -> CSS 字串。

.EXAMPLES
    # 1. 直接修改 CSS 檔案中的顏色
    Update-CssClassProperty -Path "app.css" -ClassName "btn" -Key "color" -Value "red"

    # 2. 將 theme.css 的 .btn-base 樣式合併到 app.css 的 .btn-primary，僅補齊缺少的屬性
    Merge-CssClass -TargetPath "app.css" -TargetClassName "btn-primary" -SourceObject "theme.css:.btn-base" -Strategy FillMissing

    # 3. 實體化重構流程
    Export-CssToEntities -CssPath "legacy.css" -OutputRoot ".\Entities"
    Update-CssEntityProperty -Path ".\Entities\legacy\old-class.json" -Key "display" -Value "flex"
    Import-CssFromEntities -SourceDir ".\Entities\legacy" -OutputFile "modern.css"
#>
# CssClassManager.psm1
# 提供安全的 CSS Class 解析、比較、移除與實體化管理功能

function Get-CssClasses {
    <#
    .SYNOPSIS
        解析 CSS 檔案並回傳 Class 定義列表。
    .DESCRIPTION
        使用狀態機 (State Machine) 解析 CSS，能正確處理嵌套結構、註解以及字串內容。
    .PARAMETER Path
        CSS 檔案的路徑。
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        Write-Error "找不到檔案: $Path"
        return
    }

    $content = Get-Content $Path -Raw
    $length = $content.Length
    $results = @()

    $index = 0
    $line = 1
    $inComment = $false
    $inString = $false
    $stringChar = $null # 記錄是單引號還是雙引號
    $buffer = ""
    $currentSelectorStart = -1
    
    # 使用 Stack 追蹤作用域: @{ Type='Root'|'Media'|'Class'|'Other'; Selector='...'; StartIndex=...; SelectorStart=... }
    $scopeStack = new-object System.Collections.Generic.Stack[PSObject]
    $scopeStack.Push([PSCustomObject]@{ Type = 'Root'; Selector = ''; StartIndex = 0; SelectorStart = 0 })

    while ($index -lt $length) {
        $char = $content[$index]
        
        # 處理行號計數
        if ($char -eq "`n") { $line++ }

        # 1. 處理註解 /* ... */ (不在字串內時)
        if (-not $inString) {
            if (-not $inComment -and $char -eq '/' -and ($index + 1 -lt $length) -and $content[$index + 1] -eq '*') {
                $inComment = $true
                $index++
            }
            elseif ($inComment -and $char -eq '*' -and ($index + 1 -lt $length) -and $content[$index + 1] -eq '/') {
                $inComment = $false
                $index += 2
                continue
            }
        }

        if ($inComment) {
            $index++
            continue
        }

        # 2. 處理字串 "..." 或 '...' (不在註解內時)
        if (-not $inComment) {
            if (-not $inString -and ($char -eq '"' -or $char -eq "'")) {
                $inString = $true
                $stringChar = $char
            }
            elseif ($inString -and $char -eq $stringChar) {
                # 檢查是否為轉義字符 (例如 content: "\"")
                $isEscaped = $false
                $backIndex = $index - 1
                while ($backIndex -ge 0 -and $content[$backIndex] -eq '\') {
                    $isEscaped = -not $isEscaped
                    $backIndex--
                }
                
                if (-not $isEscaped) {
                    $inString = $false
                    $stringChar = $null
                }
            }
        }

        # 3. 狀態機邏輯 (忽略字串內的符號)
        if ($inString) {
            # 字串內的內容只加入 buffer，不觸發狀態變更
            if ($currentSelectorStart -eq -1) {
                # 理論上不該發生，除非 CSS 語法錯誤（字串出現在選擇器外層且無前導）
                # 但為了保險，若 buffer 開始累積，標記起始點
                if ([String]::IsNullOrWhiteSpace($buffer) -and -not [String]::IsNullOrWhiteSpace($char)) {
                    $currentSelectorStart = $index
                }
            }
            $buffer += $char
        }
        elseif ($char -eq '{') {
            $selector = $buffer.Trim()
            $buffer = ""
            
            $type = 'Other'
            if ($selector.StartsWith("@")) {
                $type = 'Media' # 簡化處理：假設所有 @-rules 都是容器或可忽略
            }
            elseif ($selector -match '\.[a-zA-Z0-9_-]+') {
                $type = 'Class'
            }
            
            # 使用追蹤到的起始索引，若無則退回當前索引
            $selStart = if ($currentSelectorStart -ne -1) { $currentSelectorStart } else { $index }
            
            $scopeStack.Push([PSCustomObject]@{
                    Type          = $type
                    Selector      = $selector
                    StartIndex    = $index # 指向 '{'
                    SelectorStart = $selStart
                })
            
            $currentSelectorStart = -1
        }
        elseif ($char -eq '}') {
            if ($scopeStack.Count -gt 1) {
                $scope = $scopeStack.Pop()
                
                if ($scope.Type -eq 'Class') {
                    # 完成一個 Class 區塊
                    $blockStart = $scope.StartIndex
                    $blockEnd = $index
                    
                    # 提取內容
                    $innerContent = $content.Substring($blockStart + 1, $blockEnd - $blockStart - 1).Trim()
                    
                    # 判斷上下文 (Media Query)
                    $context = $null
                    if ($scopeStack.Peek().Type -eq 'Media') {
                        $context = $scopeStack.Peek().Selector
                    }
                    
                    # 提取 Class 名稱
                    $classNames = [regex]::Matches($scope.Selector, '\.([a-zA-Z0-9_-]+)') | ForEach-Object { $_.Groups[1].Value }
                    
                    foreach ($name in $classNames) {
                        $results += [PSCustomObject]@{
                            ClassName  = $name
                            Selector   = $scope.Selector
                            Content    = $innerContent
                            Context    = $context
                            File       = $Path
                            StartIndex = $scope.SelectorStart # 選擇器的確切起始位置
                            BlockEnd   = $index # 區塊結束符 '}' 的確切位置
                        }
                    }
                }
            }
            $buffer = ""
            $currentSelectorStart = -1
        }
        else {
            # 記錄選擇器的起始位置（忽略前導空白）
            if ([String]::IsNullOrWhiteSpace($buffer) -and -not [String]::IsNullOrWhiteSpace($char)) {
                $currentSelectorStart = $index
            }
            $buffer += $char
        }

        $index++
    }

    return $results
}

function Compare-CssStyle {
    <#
    .SYNOPSIS
        比較兩個 CSS 樣式區塊是否在語義上相同。
    .DESCRIPTION
        將 CSS 內容正規化（移除註解、空白、排序屬性）後進行比較。
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$StyleA,
        
        [Parameter(Mandatory = $true)]
        [string]$StyleB
    )

    function Get-NormalizedCss($css) {
        # 移除註解
        $css = $css -replace '/\*[\s\S]*?\*/', ''
        
        # 正規化空白
        $css = $css -replace '\s+', ' '
        
        # 分割屬性
        $props = $css.Split(';') | 
        ForEach-Object { $_.Trim() } | 
        Where-Object { $_ -ne "" } |
        ForEach-Object {
            # 正規化屬性間距 (例如 "color:red" -> "color: red")
            if ($_ -match '^([^:]+):(.+)$') {
                $key = $matches[1].Trim().ToLower()
                $val = $matches[2].Trim()
                "$($key): $val"
            }
            else {
                $_
            }
        } |
        Sort-Object # 依字母順序排序屬性
            
        return $props -join ';'
    }

    $normA = Get-NormalizedCss $StyleA
    $normB = Get-NormalizedCss $StyleB

    $isMatch = $normA -eq $normB
    
    return [PSCustomObject]@{
        IsIdentical = $isMatch
        NormalizedA = $normA
        NormalizedB = $normB
    }
}

function Remove-CssClass {
    <#
    .SYNOPSIS
        安全地從檔案中移除 CSS Class 定義。
    .DESCRIPTION
        建立備份，使用解析器提供的精確索引移除 Class 區塊，並驗證大括號平衡。
        若選擇器包含多個 Class（群組選擇器），則拒絕移除以策安全。
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        
        [Parameter(Mandatory = $true)]
        [string]$ClassName,
        
        [string]$SelectorOverride = $null
    )

    if (-not (Test-Path $Path)) {
        Write-Error "找不到檔案: $Path"
        return $false
    }

    # 1. 建立備份
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $backupPath = "$Path.safe_backup_$timestamp"
    Copy-Item $Path $backupPath -Force
    Write-Verbose "已建立備份於 $backupPath"

    try {
        $content = Get-Content $Path -Raw
        
        # 2. 使用解析器定位區塊
        $classes = Get-CssClasses -Path $Path
        
        # 篩選目標 Class
        $targetBlock = $null
        if ($SelectorOverride) {
            $targetBlock = $classes | Where-Object { $_.Selector -eq $SelectorOverride } | Select-Object -First 1
        }
        else {
            $targetBlock = $classes | Where-Object { $_.ClassName -eq $ClassName } | Select-Object -First 1
        }
        
        if (-not $targetBlock) {
            Write-Warning "在 $Path 中找不到 Class .$ClassName"
            return $false
        }
        
        if ($targetBlock.StartIndex -eq -1) {
            Write-Error "無法確定 Class 區塊的確切位置。"
            return $false
        }

        # 安全檢查：群組選擇器
        if ($targetBlock.Selector -match ',') {
            Write-Warning "安全防護：Class .$ClassName 屬於群組選擇器 '$($targetBlock.Selector)'。"
            Write-Warning "目前不支援部分移除群組選擇器，請手動處理。"
            return $false
        }
        
        # 3. 移除內容
        $removeStartIndex = $targetBlock.StartIndex
        $removeEndIndex = $targetBlock.BlockEnd
        
        # 檢查前導換行/空白以移除空行
        while ($removeStartIndex -gt 0) {
            $prevChar = $content[$removeStartIndex - 1]
            if ([String]::IsNullOrWhiteSpace($prevChar)) {
                $removeStartIndex--
            }
            else {
                break
            }
        }

        $newContent = $content.Remove($removeStartIndex, ($removeEndIndex - $removeStartIndex + 1))
        
        # 插入註解標記
        $comment = "`n/* .$ClassName removed by CssClassManager */`n"
        $newContent = $newContent.Insert($removeStartIndex, $comment)
        
        # 4. 驗證完整性 (檢查大括號平衡)
        $openCount = ($newContent.ToCharArray() | Where-Object { $_ -eq '{' }).Count
        $closeCount = ($newContent.ToCharArray() | Where-Object { $_ -eq '}' }).Count
        
        if ($openCount -ne $closeCount) {
            throw "移除後偵測到大括號不匹配！ (Open: $openCount, Close: $closeCount)"
        }
        
        # 5. 儲存檔案
        $newContent | Set-Content $Path -NoNewline
        Write-Output "成功從 $Path 移除 .$ClassName" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Error "移除失敗: $_"
        Write-Warning "正在從備份還原..."
        Copy-Item $backupPath $Path -Force
        return $false
    }
}

function ConvertTo-CssJson {
    <#
    .SYNOPSIS
        將 CSS Class 物件轉換為 JSON 格式物件。
    .DESCRIPTION
        接受 Get-CssClasses 回傳的物件，解析並排序屬性，轉換為標準化的 JSON 結構。
    .PARAMETER CssClass
        來自 Get-CssClasses 的 CSS Class 物件。
    .PARAMETER SourceFileName
        （選用）來源檔案名稱，用於 metadata。
    #>
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [PSCustomObject]$CssClass,
        
        [string]$SourceFileName = $null
    )

    process {
        # 解析屬性並排序
        $rawContent = $CssClass.Content
        
        # 移除註解以進行純屬性解析
        $cleanContent = $rawContent -replace '/\*[\s\S]*?\*/', ''
        
        $properties = [Ordered]@{}
        
        $cleanContent.Split(';') | ForEach-Object {
            $parts = $_.Split(':')
            if ($parts.Count -ge 2) {
                $key = $parts[0].Trim().ToLower()
                # 重新組合值（防止值裡面有冒號，如 url(http://...)）
                $value = ($parts[1..($parts.Count - 1)] -join ':').Trim()
                
                if (-not [String]::IsNullOrWhiteSpace($key)) {
                    $properties[$key] = $value
                }
            }
        }
        
        # 對屬性鍵值進行排序
        $sortedProps = [Ordered]@{}
        $properties.Keys | Sort-Object | ForEach-Object {
            $sortedProps[$_] = $properties[$_]
        }
        
        # 決定來源檔案名稱
        $source = $SourceFileName
        if (-not $source -and $CssClass.File) {
            $source = [System.IO.Path]::GetFileNameWithoutExtension($CssClass.File)
        }
        
        # 建構 JSON 物件
        # 注意：這裡 properties 保持為 OrderedDictionary，ConvertTo-Json 能正確處理
        $jsonObject = [Ordered]@{
            name       = $CssClass.ClassName
            selector   = $CssClass.Selector
            properties = $sortedProps
            metadata   = @{
                sourceFile = $source
                context    = $CssClass.Context
            }
        }
        
        return [PSCustomObject]$jsonObject
    }
}

function ConvertFrom-CssJson {
    <#
    .SYNOPSIS
        將 JSON 格式物件轉換為 CSS 字串。
    .DESCRIPTION
        接受標準化的 JSON 物件，組合成格式化的 CSS 規則字串。
    .PARAMETER JsonObject
        包含 name, selector, properties, metadata 的 JSON 物件。
    #>
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [PSCustomObject]$JsonObject
    )

    process {
        $selector = $JsonObject.selector
        $props = $JsonObject.properties
        
        $propLines = @()
        
        # 處理 properties
        # 如果是 PSCustomObject (來自 JSON 反序列化)，使用 .PSObject.Properties
        # 如果是 OrderedDictionary (來自記憶體中的 ConvertTo-CssJson)，使用 .Keys
        if ($props -is [System.Management.Automation.PSCustomObject]) {
            foreach ($prop in $props.PSObject.Properties) {
                $key = $prop.Name
                $val = $prop.Value
                $propLines += "    ${key}: $val;"
            }
        }
        elseif ($props -is [System.Collections.IDictionary]) {
            foreach ($key in $props.Keys) {
                $val = $props[$key]
                $propLines += "    ${key}: $val;"
            }
        }
        
        return "$selector {`n" + ($propLines -join "`n") + "`n}"
    }
}

function Export-CssToEntities {
    <#
    .SYNOPSIS
        將 CSS 檔案實體化為 JSON 檔案集合。
    .DESCRIPTION
        解析 CSS 並將每個 Class 轉換為獨立的 JSON 檔案，屬性自動排序。
        支援清理模式以管理既有實體。
    .PARAMETER CssPath
        來源 CSS 檔案路徑。
    .PARAMETER OutputRoot
        實體輸出根目錄。
    .PARAMETER CleanMode
        清理模式：Keep (保留), DeleteAll (全刪), KeepSoftDeleted (保留軟刪除)。
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$CssPath,
        
        [string]$OutputRoot = ".\CssEntities",
        
        [ValidateSet("Keep", "DeleteAll", "KeepSoftDeleted")]
        [string]$CleanMode = "Keep"
    )

    # 1. 準備輸出目錄
    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($CssPath)
    $targetDir = Join-Path $OutputRoot $fileName

    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }
    else {
        # 處理清理模式
        if ($CleanMode -eq "DeleteAll") {
            Write-Verbose "清理模式: DeleteAll - 刪除所有現有 JSON..." -ForegroundColor Yellow
            Get-ChildItem -Path $targetDir -Filter "*.json" | Remove-Item -Force
        }
        elseif ($CleanMode -eq "KeepSoftDeleted") {
            Write-Verbose "清理模式: KeepSoftDeleted - 保留軟刪除檔案 (_*)，刪除其他..." -ForegroundColor Yellow
            Get-ChildItem -Path $targetDir -Filter "*.json" | Where-Object { -not $_.Name.StartsWith("_") } | Remove-Item -Force
        }
    }

    Write-Verbose "正在將 $CssPath 實體化至 $targetDir ..." -ForegroundColor Cyan

    # 2. 解析 CSS
    $classes = Get-CssClasses -Path $CssPath

    foreach ($cls in $classes) {
        # 3. 使用轉換函數
        $entity = $cls | ConvertTo-CssJson -SourceFileName $fileName

        # 4. 寫入 JSON
        $jsonFileName = "$($cls.ClassName).json"
        $jsonPath = Join-Path $targetDir $jsonFileName
        
        # 處理重名衝突 (例如在不同 media query 中)
        $counter = 1
        while (Test-Path $jsonPath) {
            $jsonFileName = "$($cls.ClassName)_$counter.json"
            $jsonPath = Join-Path $targetDir $jsonFileName
            $counter++
        }
        
        $entity | ConvertTo-Json -Depth 5 | Set-Content $jsonPath -Encoding UTF8
        Write-Verbose "Created: $jsonFileName"
    }

    Write-Output "完成！共匯出 $($classes.Count) 個實體。" -ForegroundColor Green
}

function Import-CssFromEntities {
    <#
    .SYNOPSIS
        從 JSON 實體集合建置 CSS 檔案。
    .DESCRIPTION
        讀取 JSON 實體並組合成 CSS 檔案，支援 Media Query 分組與軟刪除過濾。
    .PARAMETER SourceDir
        實體來源目錄。
    .PARAMETER OutputFile
        輸出 CSS 檔案路徑。
    .PARAMETER IncludeSoftDeleted
        是否包含軟刪除的實體（檔名以 _ 開頭）。
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDir,
        
        [Parameter(Mandatory = $true)]
        [string]$OutputFile,
        
        [switch]$IncludeSoftDeleted
    )

    Write-Host "正在從 $SourceDir 建置 CSS ..." -ForegroundColor Cyan

    if (-not (Test-Path $SourceDir)) {
        Write-Error "找不到來源目錄: $SourceDir"
        return
    }

    # 1. 掃描並讀取 JSON
    $files = Get-ChildItem -Path $SourceDir -Filter "*.json"
    $entities = @()

    foreach ($file in $files) {
        # 軟刪除檢查
        if (-not $IncludeSoftDeleted -and $file.Name.StartsWith("_")) {
            Write-Verbose "Skipping deleted entity: $($file.Name)"
            continue
        }

        try {
            $json = Get-Content $file.FullName -Raw | ConvertFrom-Json
            $entities += $json
        }
        catch {
            Write-Error "Failed to parse $($file.Name): $_"
        }
    }

    # 2. 分組 (Grouping by Context)
    $grouped = $entities | Group-Object -Property @{Expression = { $_.metadata.context } }

    # 準備輸出內容
    $cssOutput = @()

    # 3. 處理 Root Context (無 Context)
    $rootGroup = $grouped | Where-Object { [String]::IsNullOrWhiteSpace($_.Name) }
    if ($rootGroup) {
        foreach ($entity in $rootGroup.Group) {
            $cssOutput += $entity | ConvertFrom-CssJson
        }
    }

    # 4. 處理其他 Context (Media Queries 等)
    $otherGroups = $grouped | Where-Object { -not [String]::IsNullOrWhiteSpace($_.Name) }
    foreach ($group in $otherGroups) {
        $context = $group.Name
        $cssOutput += "`n$context {"
        
        foreach ($entity in $group.Group) {
            # Indent inner rules
            $rule = $entity | ConvertFrom-CssJson
            # Add extra indentation
            $indentedRule = $rule -split "`n" | ForEach-Object { "    $_" }
            $cssOutput += ($indentedRule -join "`n")
        }
        
        $cssOutput += "}"
    }

    # 5. 寫入檔案
    $finalCss = $cssOutput -join "`n`n"
    $finalCss | Set-Content $OutputFile -Encoding UTF8

    Write-Output "建置完成！輸出至: $OutputFile" -ForegroundColor Green
    Write-Output "共處理 $($entities.Count) 個實體。" -ForegroundColor Green
}

function Get-CssEntity {
    <#
    .SYNOPSIS
        讀取並解析 CSS 實體 JSON 檔案。
    .DESCRIPTION
        讀取 JSON 檔案並轉換為 PowerShell 物件，確保 properties 為可操作的 OrderedDictionary。
    .PARAMETER Path
        JSON 檔案路徑。
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        throw "找不到檔案: $Path"
    }

    $json = Get-Content $Path -Raw | ConvertFrom-Json
    
    # 確保 properties 是 OrderedDictionary 以便操作
    $props = [Ordered]@{}
    if ($json.properties) {
        foreach ($prop in $json.properties.PSObject.Properties) {
            $props[$prop.Name] = $prop.Value
        }
    }
    
    # 重建物件以包含可編輯的 properties
    $entity = [Ordered]@{
        name       = $json.name
        selector   = $json.selector
        properties = $props
        metadata   = $json.metadata
    }
    
    return [PSCustomObject]$entity
}

function Update-CssEntityProperty {
    <#
    .SYNOPSIS
        修改 CSS 實體的屬性。
    .DESCRIPTION
        新增、更新或刪除實體的 CSS 屬性，並自動排序後存檔。
    .PARAMETER Path
        JSON 實體檔案路徑。
    .PARAMETER Key
        CSS 屬性名稱 (例如 'color')。
    .PARAMETER Value
        CSS 屬性值 (例如 'red')。若 Action 為 Remove 則忽略。
    .PARAMETER Action
        操作類型：Set (新增/更新), Remove (刪除)。
    .PARAMETER Force
        若為 Set 且屬性已存在，是否強制覆寫。預設為 $true。
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        
        [Parameter(Mandatory = $true)]
        [string]$Key,
        
        [string]$Value,
        
        [ValidateSet("Set", "Remove")]
        [string]$Action = "Set",
        
        [switch]$Force = $true
    )

    $entity = Get-CssEntity -Path $Path
    $props = $entity.properties
    $modified = $false
    $Key = $Key.ToLower().Trim()

    if ($Action -eq "Set") {
        if ($props.Contains($Key)) {
            if ($Force) {
                if ($props[$Key] -ne $Value) {
                    $props[$Key] = $Value
                    $modified = $true
                    Write-Verbose "Updated '$Key' to '$Value'"
                }
            }
            else {
                Write-Warning "屬性 '$Key' 已存在且 Force 為 false，跳過更新。"
            }
        }
        else {
            $props[$Key] = $Value
            $modified = $true
            Write-Verbose "Added '$Key' = '$Value'"
        }
    }
    elseif ($Action -eq "Remove") {
        if ($props.Contains($Key)) {
            $props.Remove($Key)
            $modified = $true
            Write-Verbose "Removed '$Key'"
        }
        else {
            Write-Warning "屬性 '$Key' 不存在，無法刪除。"
        }
    }

    if ($modified) {
        # 重新排序屬性
        $sortedProps = [Ordered]@{}
        $props.Keys | Sort-Object | ForEach-Object {
            $sortedProps[$_] = $props[$_]
        }
        $entity.properties = $sortedProps
        
        # 存檔
        $entity | ConvertTo-Json -Depth 5 | Set-Content $Path -Encoding UTF8
        Write-Output "已更新實體: $Path" -ForegroundColor Green
    }
    else {
        Write-Output "實體未變更: $Path" -ForegroundColor Gray
    }
}

function Merge-CssEntity {
    <#
    .SYNOPSIS
        合併兩個 CSS 實體。
    .DESCRIPTION
        將來源實體的屬性合併到目標實體，支援多種合併策略。
    .PARAMETER TargetPath
        目標 JSON 實體檔案路徑 (將被修改)。
    .PARAMETER SourcePath
        來源 JSON 實體檔案路徑 (唯讀)。
    .PARAMETER Strategy
        合併策略：
        - Overwrite: 來源屬性覆蓋目標屬性。
        - FillMissing: 僅新增目標缺少的屬性。
        - PruneDuplicate: 若目標屬性與來源相同，則從目標中刪除 (用於提取共用樣式)。
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$TargetPath,
        
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,
        
        [ValidateSet("Overwrite", "FillMissing", "PruneDuplicate")]
        [string]$Strategy = "Overwrite"
    )

    $target = Get-CssEntity -Path $TargetPath
    $source = Get-CssEntity -Path $SourcePath
    
    $targetProps = $target.properties
    $sourceProps = $source.properties
    $modified = $false

    foreach ($key in $sourceProps.Keys) {
        $val = $sourceProps[$key]
        
        if ($Strategy -eq "Overwrite") {
            if (-not $targetProps.Contains($key) -or $targetProps[$key] -ne $val) {
                $targetProps[$key] = $val
                $modified = $true
                Write-Verbose "Overwrote '$key'"
            }
        }
        elseif ($Strategy -eq "FillMissing") {
            if (-not $targetProps.Contains($key)) {
                $targetProps[$key] = $val
                $modified = $true
                Write-Verbose "Filled '$key'"
            }
        }
        elseif ($Strategy -eq "PruneDuplicate") {
            if ($targetProps.Contains($key) -and $targetProps[$key] -eq $val) {
                $targetProps.Remove($key)
                $modified = $true
                Write-Verbose "Pruned duplicate '$key'"
            }
        }
    }

    if ($modified) {
        # 重新排序
        $sortedProps = [Ordered]@{}
        $targetProps.Keys | Sort-Object | ForEach-Object {
            $sortedProps[$_] = $targetProps[$_]
        }
        $target.properties = $sortedProps
        
        $target | ConvertTo-Json -Depth 5 | Set-Content $TargetPath -Encoding UTF8
        Write-Output "已合併實體 ($Strategy): $TargetPath" -ForegroundColor Green
    }
    else {
        Write-Output "實體未變更: $TargetPath" -ForegroundColor Gray
    }
}

function Replace-CssBlock {
    <#
    .SYNOPSIS
        (私有) 替換 CSS 檔案中的指定區塊。
    #>
    param(
        [string]$Path,
        [int]$StartIndex,
        [int]$EndIndex,
        [string]$NewContent
    )

    # 建立備份
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $backupPath = "$Path.safe_backup_$timestamp"
    Copy-Item $Path $backupPath -Force

    try {
        $content = Get-Content $Path -Raw
        
        # 移除舊內容
        $lengthToRemove = $EndIndex - $StartIndex + 1
        $tempContent = $content.Remove($StartIndex, $lengthToRemove)
        
        # 插入新內容
        $finalContent = $tempContent.Insert($StartIndex, $NewContent)
        
        # 寫入檔案
        $finalContent | Set-Content $Path -NoNewline -Encoding UTF8
        return $true
    }
    catch {
        Write-Error "替換失敗: $_"
        Copy-Item $backupPath $Path -Force
        return $false
    }
}

function Update-CssClassProperty {
    <#
    .SYNOPSIS
        直接修改 CSS 檔案中的 Class 屬性。
    .DESCRIPTION
        讀取 CSS 檔案，定位指定 Class，修改屬性後寫回。
    .PARAMETER Path
        CSS 檔案路徑。
    .PARAMETER ClassName
        Class 名稱 (不含點號)。
    .PARAMETER Key
        屬性名稱。
    .PARAMETER Value
        屬性值。
    .PARAMETER Action
        Set 或 Remove。
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        
        [Parameter(Mandatory = $true)]
        [string]$ClassName,
        
        [Parameter(Mandatory = $true)]
        [string]$Key,
        
        [string]$Value,
        
        [ValidateSet("Set", "Remove")]
        [string]$Action = "Set",
        
        [switch]$Force = $true
    )

    if (-not (Test-Path $Path)) { throw "找不到檔案: $Path" }

    # 1. 定位 Class
    $classes = Get-CssClasses -Path $Path
    $target = $classes | Where-Object { $_.ClassName -eq $ClassName } | Select-Object -First 1
    
    if (-not $target) {
        Write-Warning "找不到 Class .$ClassName"
        return
    }

    # 2. 轉換為物件並修改
    $entity = $target | ConvertTo-CssJson
    $props = $entity.properties
    $modified = $false
    $Key = $Key.ToLower().Trim()

    if ($Action -eq "Set") {
        if ($props.Contains($Key)) {
            if ($Force -and $props[$Key] -ne $Value) {
                $props[$Key] = $Value
                $modified = $true
            }
        }
        else {
            $props[$Key] = $Value
            $modified = $true
        }
    }
    elseif ($Action -eq "Remove") {
        if ($props.Contains($Key)) {
            $props.Remove($Key)
            $modified = $true
        }
    }

    if ($modified) {
        # 3. 轉回 CSS 字串
        # 重新排序
        $sortedProps = [Ordered]@{}
        $props.Keys | Sort-Object | ForEach-Object { $sortedProps[$_] = $props[$_] }
        $entity.properties = $sortedProps
        
        $newCss = $entity | ConvertFrom-CssJson
        
        # 4. 替換檔案內容
        # 注意：Get-CssClasses 的 StartIndex 是選擇器起點，BlockEnd 是結尾大括號
        # ConvertFrom-CssJson 生成的是完整的 "selector { ... }"
        # 所以我們直接替換整個區塊
        
        Replace-CssBlock -Path $Path -StartIndex $target.StartIndex -EndIndex $target.BlockEnd -NewContent $newCss
        Write-Output "已更新 CSS Class: .$ClassName" -ForegroundColor Green
    }
    else {
        Write-Output "未變更: .$ClassName" -ForegroundColor Gray
    }
}

function Merge-CssClass {
    <#
    .SYNOPSIS
        將外部定義合併到 CSS 檔案中的 Class。
    .DESCRIPTION
        從另一個 CSS 檔案或 JSON 實體讀取屬性，合併到目標 CSS 檔案的 Class 中。
    .PARAMETER TargetPath
        目標 CSS 檔案路徑。
    .PARAMETER TargetClassName
        目標 Class 名稱。
    .PARAMETER SourceObject
        來源物件 (可以是 JSON 檔案路徑，或另一個 CSS 檔案路徑+ClassName)。
        若是 CSS 檔案，格式為 "path/to/file.css:.className"
    .PARAMETER Strategy
        Overwrite, FillMissing, PruneDuplicate
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$TargetPath,
        
        [Parameter(Mandatory = $true)]
        [string]$TargetClassName,
        
        [Parameter(Mandatory = $true)]
        [string]$SourceObject,
        
        [ValidateSet("Overwrite", "FillMissing", "PruneDuplicate")]
        [string]$Strategy = "Overwrite"
    )

    # 1. 取得目標 Class
    $classes = Get-CssClasses -Path $TargetPath
    $targetClass = $classes | Where-Object { $_.ClassName -eq $TargetClassName } | Select-Object -First 1
    if (-not $targetClass) { throw "目標 Class .$TargetClassName 不存在" }
    
    $targetEntity = $targetClass | ConvertTo-CssJson
    $targetProps = $targetEntity.properties

    # 2. 取得來源屬性
    $sourceProps = $null
    
    if ($SourceObject.EndsWith(".json")) {
        # 來源是 JSON 實體
        $sourceEntity = Get-CssEntity -Path $SourceObject
        $sourceProps = $sourceEntity.properties
    }
    elseif ($SourceObject -match '(.+\.css):\.?(.+)') {
        # 來源是 CSS 檔案中的 Class
        $srcFile = $matches[1]
        $srcClass = $matches[2]
        $srcClasses = Get-CssClasses -Path $srcFile
        $srcTarget = $srcClasses | Where-Object { $_.ClassName -eq $srcClass } | Select-Object -First 1
        if ($srcTarget) {
            $srcEntity = $srcTarget | ConvertTo-CssJson
            $sourceProps = $srcEntity.properties
        }
    }
    
    if (-not $sourceProps) { throw "無法讀取來源物件: $SourceObject" }

    # 3. 合併邏輯
    $modified = $false
    foreach ($key in $sourceProps.Keys) {
        $val = $sourceProps[$key]
        
        if ($Strategy -eq "Overwrite") {
            if (-not $targetProps.Contains($key) -or $targetProps[$key] -ne $val) {
                $targetProps[$key] = $val
                $modified = $true
            }
        }
        elseif ($Strategy -eq "FillMissing") {
            if (-not $targetProps.Contains($key)) {
                $targetProps[$key] = $val
                $modified = $true
            }
        }
        elseif ($Strategy -eq "PruneDuplicate") {
            if ($targetProps.Contains($key) -and $targetProps[$key] -eq $val) {
                $targetProps.Remove($key)
                $modified = $true
            }
        }
    }

    # 4. 寫回
    if ($modified) {
        $sortedProps = [Ordered]@{}
        $targetProps.Keys | Sort-Object | ForEach-Object { $sortedProps[$_] = $targetProps[$_] }
        $targetEntity.properties = $sortedProps
        
        $newCss = $targetEntity | ConvertFrom-CssJson
        Replace-CssBlock -Path $TargetPath -StartIndex $targetClass.StartIndex -EndIndex $targetClass.BlockEnd -NewContent $newCss
        Write-Output "已合併 CSS Class: .$TargetClassName ($Strategy)" -ForegroundColor Green
    }
    else {
        Write-Output "未變更: .$TargetClassName" -ForegroundColor Gray
    }
}

Export-ModuleMember -Function Get-CssClasses, Compare-CssStyle, Remove-CssClass, ConvertTo-CssJson, ConvertFrom-CssJson, Export-CssToEntities, Import-CssFromEntities, Get-CssEntity, Update-CssEntityProperty, Merge-CssEntity, Update-CssClassProperty, Merge-CssClass

