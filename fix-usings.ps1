$base = "C:\Users\Aaron\source\PenguinTwitchBot"
$usingRemovals = @{
    "ToggleCommandDisabledType.cs" = "using PenguinTwitchBot.Bot.Commands;"
    "TimerGroupSetEnabledStateType.cs" = "using PenguinTwitchBot.Bot.Commands.Misc;"
    "PointCommandType.cs" = "using PenguinTwitchBot.Bot.Core.Points;"
    "GiftPointsType.cs" = "using PenguinTwitchBot.Bot.Core.Points;"
    "ChannelPointSetEnabledStateType.cs" = "using PenguinTwitchBot.Bot.TwitchServices;"
}

foreach ($file in $usingRemovals.Keys) {
    $path = "$base\PenguinTwitchBot.Database\Bot\Actions\SubActions\Types\$file"
    if (Test-Path $path) {
        $content = [System.IO.File]::ReadAllText($path)
        $content = $content -replace [regex]::Escape($usingRemovals[$file]) + "`r`n", ''
        [System.IO.File]::WriteAllText($path, $content, [System.Text.UTF8Encoding]::new($false))
        Write-Host "Removed using from $file"
    }
}
