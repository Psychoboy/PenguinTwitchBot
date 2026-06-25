$base = "C:\Users\Aaron\source\PenguinTwitchBot"
Get-ChildItem "$base\PenguinTwitchBot.Database\Bot\Actions\SubActions\Types" -Filter "*.cs" | ForEach-Object {
    $path = $_.FullName
    $lines = [System.IO.File]::ReadAllLines($path)
    $changed = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^(\s+)public List<SubActionUIField> GetUIFields') {
            $leading = $matches[1]
            if ($leading.Length -ne 8) {
                $lines[$i] = "        public List<SubActionUIField> GetUIFields" + $lines[$i].Substring($leading.Length + "public List<SubActionUIField> GetUIFields".Length)
                $changed = $true
            }
        }
    }
    if ($changed) {
        [System.IO.File]::WriteAllLines($path, $lines, [System.Text.UTF8Encoding]::new($false))
        Write-Host "Fixed indentation in $($_.Name)"
    }
}
