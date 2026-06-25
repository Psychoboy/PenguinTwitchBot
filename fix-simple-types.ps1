$base = "C:\Users\Aaron\source\PenguinTwitchBot"

# Build MdiIcons map
$mdiMap = @{}
Get-Content "$base\PenguinTwitchBot\Bot\Actions\SubActions\MdiIcons.cs" | ForEach-Object {
    if ($_ -match 'public const string (\w+) = "([^"]+)"') {
        $mdiMap[$matches[1]] = $matches[2]
    }
}

$simpleTypes = @(
    "ToggleCommandDisabledType",
    "TimerGroupSetEnabledStateType",
    "PointCommandType",
    "GiftPointsType",
    "ExecuteActionType",
    "ExecuteDefaultCommandType",
    "ChannelPointSetEnabledStateType",
    "CheckPointsType"
)

$usingRemovals = @{
    "ToggleCommandDisabledType" = "using PenguinTwitchBot.Bot.Commands;"
    "TimerGroupSetEnabledStateType" = "using PenguinTwitchBot.Bot.Commands.Misc;"
    "PointCommandType" = "using PenguinTwitchBot.Bot.Core.Points;"
    "GiftPointsType" = "using PenguinTwitchBot.Bot.Core.Points;"
    "ChannelPointSetEnabledStateType" = "using PenguinTwitchBot.Bot.TwitchServices;"
}

foreach ($type in $simpleTypes) {
    $file = "$base\PenguinTwitchBot.Database\Bot\Actions\SubActions\Types\$type.cs"
    if (-not (Test-Path $file)) { continue }
    
    $lines = [System.IO.File]::ReadAllLines($file)
    $newLines = @()
    $skipUntilClosingBrace = $false
    $braceDepth = 0
    $foundGetUIFields = $false
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        
        # Replace MdiIcons refs
        foreach ($k in $mdiMap.Keys) {
            $line = $line -replace "MdiIcons\.$k", "`"$($mdiMap[$k])`""
        }
        
        # Track GetUIFields method
        if ($line -match 'public List<SubActionUIField> GetUIFields') {
            $foundGetUIFields = $true
            $skipUntilClosingBrace = $true
            $braceDepth = 0
            # Count braces on this line
            $braceDepth += ([regex]::Matches($line, '\{')).Count
            $braceDepth -= ([regex]::Matches($line, '\}')).Count
            # Add the replacement method
            $newLines += "        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)"
            $newLines += "        {"
            $newLines += "            return [];"
            $newLines += "        }"
            continue
        }
        
        if ($skipUntilClosingBrace) {
            $braceDepth += ([regex]::Matches($line, '\{')).Count
            $braceDepth -= ([regex]::Matches($line, '\}')).Count
            if ($braceDepth -le 0) {
                $skipUntilClosingBrace = $false
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
