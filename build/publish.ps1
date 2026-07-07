# Publishes the bridge as a self-contained 32-bit Windows executable.
# proRFL.dll is 32-bit — win-x86 is mandatory, not a preference.
# Run from anywhere; output lands in <repo>/publish/win-x86.

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

dotnet publish "$repoRoot\src\Ezeeroom.KeycardBridge\Ezeeroom.KeycardBridge.csproj" `
    --configuration Release `
    --runtime win-x86 `
    --self-contained true `
    --output "$repoRoot\publish\win-x86"

Write-Host "Published to $repoRoot\publish\win-x86"
Write-Host "Next: build the installer with  iscc $repoRoot\installer\EzeeroomKeycardBridge.iss"
