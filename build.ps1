param(
    [string]$TLDPath = "C:\Program Files (x86)\Steam\steamapps\common\TheLongDark"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ModProject = Join-Path $ProjectRoot "Mod\FarmhouseWardrobeFix.csproj"
$ModOutput = Join-Path $ProjectRoot "bin\Release\net6.0"
$EmbeddedPatch = Join-Path $ProjectRoot "Mod\Resources\farmhouse_lightmap_regions.fwbc7"
$Go = Join-Path $ProjectRoot "Go"
$ExpectedPatchLength = 30808
$ExpectedPatchSha256 = "03A6B71465084881E3F4DB23B6BA0D7D4FAEF3DC929E9FB7165C4DDDF8055E7C"

if (-not (Test-Path $TLDPath)) {
    throw "The Long Dark folder was not found: $TLDPath"
}

$ModSettingsDll = Join-Path $TLDPath "Mods\ModSettings.dll"
if (-not (Test-Path -LiteralPath $ModSettingsDll)) {
    throw "ModSettings.dll was not found: $ModSettingsDll`nInstall ModSettings before building this project."
}

if (-not (Test-Path -LiteralPath $EmbeddedPatch)) {
    throw "Embedded BC7 region patch source was not found: $EmbeddedPatch"
}

$PatchFile = Get-Item -LiteralPath $EmbeddedPatch
if ($PatchFile.Length -ne $ExpectedPatchLength) {
    throw "Embedded BC7 region patch has $($PatchFile.Length) bytes; expected $ExpectedPatchLength."
}

$PatchHash = (Get-FileHash -LiteralPath $EmbeddedPatch -Algorithm SHA256).Hash
if ($PatchHash -ne $ExpectedPatchSha256) {
    throw "Embedded BC7 region patch SHA-256 mismatch: $PatchHash"
}

Write-Host "Building Farmhouse Wardrobe Fix 1.0.0 (single DLL)..."
dotnet clean $ModProject -c Release -p:TLDPath="$TLDPath"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet clean failed with exit code $LASTEXITCODE."
}

dotnet build $ModProject -c Release -p:TLDPath="$TLDPath"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

$DllSource = Join-Path $ModOutput "FarmhouseWardrobeFix.dll"
if (-not (Test-Path $DllSource)) {
    throw "Build completed without the expected DLL: $DllSource"
}

[IO.Directory]::CreateDirectory($Go) | Out-Null
$ReadyDll = Join-Path $Go "FarmhouseWardrobeFix.dll"
$ObsoletePatch = Join-Path $Go "farmhouse_lightmap_regions.fwbc7"
if ([IO.File]::Exists($ObsoletePatch)) {
    [IO.File]::Delete($ObsoletePatch)
}
Copy-Item -LiteralPath $DllSource -Destination $ReadyDll -Force

Write-Host "Build complete. Copy this one file into TheLongDark\Mods:"
Get-Item -LiteralPath $ReadyDll |
    Select-Object FullName, Length, LastWriteTime
