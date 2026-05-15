#Requires -Version 5
# Uruchamia digiWORK SmartHUB z listy zainstalowanych aplikacji
$app = Get-StartApps | Where-Object { $_.Name -eq 'digiWORK SmartHUB' } | Select-Object -First 1
if ($app) {
    Start-Process "explorer.exe" "shell:AppsFolder\$($app.AppId)"
} else {
    # Fallback: otwieramy folder Aplikacje w Eksploratorze
    Start-Process "explorer.exe" "shell:AppsFolder"
}
