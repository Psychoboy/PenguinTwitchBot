$base = "C:\Users\Aaron\source\PenguinTwitchBot"

# Build MdiIcons map
$mdiMap = @{}
Get-Content "$base\PenguinTwitchBot\Bot\Actions\SubActions\MdiIcons.cs" | ForEach-Object {
    if ($_ -match 'public const string (\w+) = "([^"]+)"') {
        $mdiMap[$matches[1]] = $matches[2]
    }
}

# Sort keys by length descending to avoid prefix collisions (PlayCircle before Play)
$sortedKeys = $mdiMap.Keys | Sort-Object Length -Descending

# Files that should return [] from GetUIFields
$emptyReturnTypes = @(
    "ToggleCommandDisabledType",
    "TimerGroupSetEnabledStateType",
    "PointCommandType",
    "GiftPointsType",
    "ExecuteActionType",
    "ExecuteDefaultCommandType",
    "ChannelPointSetEnabledStateType",
    "CheckPointsType"
)

# Files that keep static fallback fields but remove service lookup
$staticFallbackTypes = @(
    "ForEachViewerType",
    "ObsSetSceneType",
    "ObsSetBrowserSourceUrlType",
    "ObsSetColorSourceColorType",
    "ObsSetImageSourceFileType",
    "ObsSetMediaSourceFileType",
    "ObsSetMediaStateType",
    "ObsSetSceneFilterStateType",
    "ObsSetSourceAudioTrackStateType",
    "ObsSetSourceFilterStateType",
    "ObsSetSourceMuteStateType",
    "ObsSetSourceVisibilityType",
    "ObsSetTextType",
    "ObsTriggerHotkeyType"
)

$usingRemovals = @{
    "ToggleCommandDisabledType" = "using PenguinTwitchBot.Bot.Commands;"
    "TimerGroupSetEnabledStateType" = "using PenguinTwitchBot.Bot.Commands.Misc;"
    "PointCommandType" = "using PenguinTwitchBot.Bot.Core.Points;"
    "GiftPointsType" = "using PenguinTwitchBot.Bot.Core.Points;"
    "ChannelPointSetEnabledStateType" = "using PenguinTwitchBot.Bot.TwitchServices;"
}

# Process empty return types
foreach ($type in $emptyReturnTypes) {
    $file = "$base\PenguinTwitchBot.Database\Bot\Actions\SubActions\Types\$type.cs"
    if (-not (Test-Path $file)) { continue }
    
    $lines = [System.IO.File]::ReadAllLines($file)
    $newLines = @()
    $skipMode = $false
    $braceDepth = 0
    $addedMethod = $false
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        
        # Replace MdiIcons refs - use sorted keys to avoid prefix collisions
        foreach ($k in $sortedKeys) {
            $line = $line -replace "MdiIcons\.$k", "`"$($mdiMap[$k])`""
        }
        
        if ($line -match 'public List<SubActionUIField> GetUIFields') {
            $skipMode = $true
            $braceDepth = 0
            $braceDepth += ([regex]::Matches($line, '\{')).Count
            $braceDepth -= ([regex]::Matches($line, '\}')).Count
            if (-not $addedMethod) {
                $newLines += "        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)"
                $newLines += "        {"
                $newLines += "            return [];"
                $newLines += "        }"
                $addedMethod = $true
            }
            continue
        }
        
        if ($skipMode) {
            $braceDepth += ([regex]::Matches($line, '\{')).Count
            $braceDepth -= ([regex]::Matches($line, '\}')).Count
            if ($braceDepth -le 0) {
                $skipMode = $false
            }
            continue
        }
        
        # Remove unused using
        if ($usingRemovals.ContainsKey($type) -and $line -eq $usingRemovals[$type]) {
            continue
        }
        
        $newLines += $line
    }
    
    [System.IO.File]::WriteAllLines($file, $newLines, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Fixed $type"
}

# Process static fallback types (Obs + ForEachViewer)
foreach ($type in $staticFallbackTypes) {
    $file = "$base\PenguinTwitchBot.Database\Bot\Actions\SubActions\Types\$type.cs"
    if (-not (Test-Path $file)) { continue }
    
    $lines = [System.IO.File]::ReadAllLines($file)
    $newLines = @()
    $skipMode = $false
    $braceDepth = 0
    $addedMethod = $false
    $removeObsConnectorUsing = $false
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        
        # Replace MdiIcons refs - use sorted keys to avoid prefix collisions
        foreach ($k in $sortedKeys) {
            $line = $line -replace "MdiIcons\.$k", "`"$($mdiMap[$k])`""
        }
        
        if ($line -match 'public List<SubActionUIField> GetUIFields') {
            $skipMode = $true
            $braceDepth = 0
            $braceDepth += ([regex]::Matches($line, '\{')).Count
            $braceDepth -= ([regex]::Matches($line, '\}')).Count
            
            if ($type -eq "ObsTriggerHotkeyType") {
                $newLines += "        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)"
                $newLines += "        {"
                $newLines += "            var hotkeyOptions = Enum.GetNames<OBSHotkey>()"
                $newLines += "                .Select(name => new SelectOption"
                $newLines += "                {"
                $newLines += "                    Value = name,"
                $newLines += "                    Name = FormatHotkeyName(name),"
                $newLines += "                })"
                $newLines += "                .ToList();"
                $newLines += ""
                $newLines += "            return"
                $newLines += "            ["
                $newLines += "                new SubActionUIField"
                $newLines += "                {"
                $newLines += "                    PropertyName = nameof(HotkeyName),"
                $newLines += "                    Label = ""Hotkey"","
                $newLines += "                    FieldType = UIFieldType.Select,"
                $newLines += "                    Required = true,"
                $newLines += "                    SelectOptions = hotkeyOptions,"
                $newLines += "                    HelperText = ""Select the OBS hotkey to trigger"""
                $newLines += "                },"
                $newLines += "                new SubActionUIField"
                $newLines += "                {"
                $newLines += "                    PropertyName = nameof(Enabled),"
                $newLines += "                    Label = ""Enabled"","
                $newLines += "                    FieldType = UIFieldType.Switch,"
                $newLines += "                    SwitchColor = ""Success"""
                $newLines += "                }"
                $newLines += "            ];"
                $newLines += "        }"
            } elseif ($type -eq "ForEachViewerType") {
                $newLines += "        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)"
                $newLines += "        {"
                $newLines += "            List<SelectOption>? actionOptions = null;"
                $newLines += ""
                $newLines += "            return"
                $newLines += "            ["
                $newLines += "                new SubActionUIField"
                $newLines += "                {"
                $newLines += "                    PropertyName = nameof(ActionId),"
                $newLines += "                    Label = ""Action to Run"","
                $newLines += "                    FieldType = UIFieldType.Select,"
                $newLines += "                    SelectOptions = actionOptions,"
                $newLines += "                    Required = true,"
                $newLines += "                    Clearable = true,"
                $newLines += "                    HelperText = ""The action to run for each viewer. The %user% variable will be set to each viewer's username."""
                $newLines += "                },"
                $newLines += "                new SubActionUIField"
                $newLines += "                {"
                $newLines += "                    PropertyName = nameof(ViewerScope),"
                $newLines += "                    Label = ""Viewer Group"","
                $newLines += "                    FieldType = UIFieldType.Select,"
                $newLines += "                    Options = [""AllViewers"", ""ActiveViewers"", ""Subscribers""],"
                $newLines += "                    Required = true,"
                $newLines += "                    HelperText = ""AllViewers: everyone currently in chat. ActiveViewers: viewers who have interacted recently. Subscribers: viewers who are subscribed to the channel currently in chat."""
                $newLines += "                },"
                $newLines += "                new SubActionUIField"
                $newLines += "                {"
                $newLines += "                    PropertyName = nameof(Enabled),"
                $newLines += "                    Label = ""Enabled"","
                $newLines += "                    FieldType = UIFieldType.Switch,"
                $newLines += "                    SwitchColor = ""Success"""
                $newLines += "                }"
                $newLines += "            ];"
                $newLines += "        }"
            } else {
                # Obs types - return just Enabled field
                $newLines += "        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)"
                $newLines += "        {"
                $newLines += "            return new List<SubActionUIField>"
                $newLines += "            {"
                $newLines += "                new SubActionUIField"
                $newLines += "                {"
                $newLines += "                    PropertyName = nameof(Enabled),"
                $newLines += "                    Label = ""Enabled"","
                $newLines += "                    FieldType = UIFieldType.Switch,"
                $newLines += "                    SwitchColor = ""Success"""
                $newLines += "                }"
                $newLines += "            };"
                $newLines += "        }"
            }
            $addedMethod = $true
            continue
        }
        
        if ($skipMode) {
            $braceDepth += ([regex]::Matches($line, '\{')).Count
            $braceDepth -= ([regex]::Matches($line, '\}')).Count
            if ($braceDepth -le 0) {
                $skipMode = $false
            }
            continue
        }
        
        # Remove ObsConnector using for Obs types
        if ($type -ne "ObsTriggerHotkeyType" -and $line -eq "using PenguinTwitchBot.Bot.ObsConnector;") {
            continue
        }
        
        $newLines += $line
    }
    
    [System.IO.File]::WriteAllLines($file, $newLines, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Fixed $type"
}
