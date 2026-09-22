# Farmhouse Wardrobe Fix 1.0.0

[Русская версия](README_RU.md)

A **The Long Dark** mod that removes the blocked wardrobe section in the
Pleasant Valley Farmhouse (`FarmHouseA`) and repairs the baked shadow left
behind. It can also persistently hide user-selected decorative objects and
reapply those removals whenever their scene is loaded.

Author: **Dwinfear + ChatGPT**

## Features

- Automatically hides the broken `OBJ_DresserTallDrawerD` in `FarmHouseA`.
- Repairs its shadow directly inside the original `Lightmap-1_comp_light`.
- Patches only eight BC7 regions instead of replacing the complete lightmap.
- Hides the aimed object with a configurable hotkey (`F4` by default).
- Stores user-selected objects in `FarmhouseWardrobeFix.json`.
- Distinguishes identical objects by scene, hierarchy path, name and position.
- Supports hotkey changes through Mod Settings or the JSON file.
- Produces no routine console output during normal operation.

The farmhouse wardrobe is built into the mod and is not written to JSON.
`RemovedObjects` contains only objects selected by the user.

## Requirements

- The Long Dark for Windows.
- A .NET 6-compatible MelonLoader installation.
- ModSettings.
- Direct3D 11 for the baked-shadow repair.

If the game uses another graphics API or the lightmap does not match the exact
expected layout, the shadow patch stops safely and writes an error to the log.
Wardrobe and user-object removal remain independent from the shadow patch.

## Installation

The ready-to-install release contains one file:

```text
FarmhouseWardrobeFix.dll
```

Copy it into:

```text
TheLongDark\Mods
```

External `farmhouse_lightmap_regions.fwbc7`, `farmhouse_lightmap.bundle` and
`farmhouse_lightmap_patched.bc7` files are not required. The verified BC7 patch
is embedded in the DLL.

## Usage

1. Aim the centre of the screen at a separate prop or decorative object.
2. Press the configured removal key (`F4` by default).
3. The object is hidden and added to the JSON file.
4. The same instance is hidden again whenever its scene is initialized.

Change the key at:

```text
Options -> Mod Settings -> Farmhouse Wardrobe Fix
```

Selecting `None` disables the hotkey without removing saved entries.

Scene roots, floors, the player and major system objects are protected from
accidental removal.

## Configuration

The first run creates:

```text
TheLongDark\Mods\FarmhouseWardrobeFix.json
```

Example:

```json
{
  "RemovalKey": "F4",
  "RemovedObjects": [
    {
      "Scene": "FarmHouseA",
      "Path": "Root/Art/Structure/Electrical/OBJ_LampE_Prefab",
      "Name": "OBJ_LampE_Prefab",
      "PositionX": -8.435,
      "PositionY": 1.652,
      "PositionZ": 1.05
    }
  ]
}
```

`RemovalKey` may also be edited while the game is closed. Use a
`UnityEngine.KeyCode` name such as `F5`, `Delete` or `Keypad0`.

To restore a user-selected object:

1. Close the game.
2. Remove its entry from `RemovedObjects`.
3. Save the JSON file and load the scene again.

## Performance

The mod does not continuously scan objects or update the lightmap:

- the built-in wardrobe is resolved with one direct `GameObject.Find` during
  scene initialization;
- all `Transform` objects are requested only while loading a scene that has
  user-created JSON entries;
- JSON is read once at startup and written only after object selection or a
  confirmed hotkey change;
- BC7 regions are updated only during the short initial mip-streaming period;
- lightmap monitoring is completely disabled once the full 2048×2048 texture
  becomes resident;
- ordinary gameplay retains only the configured key check and one inexpensive
  startup-monitoring flag check; no scans or texture writes remain.

The embedded patch is approximately 30 KB. The mod creates no background
threads, coroutines or periodic scene scans.

## Building from source

Close the game, open PowerShell in the project root and run:

```powershell
.\build.ps1
```

For a custom installation path:

```powershell
.\build.ps1 -TLDPath "D:\Games\TheLongDark"
```

The ready DLL is written to `Go`.

## Source layout

```text
FarmhouseWardrobeFix-v1.0.0\
├─ bin\                         compiler output
├─ obj\                         MSBuild intermediate files
├─ Mod\
│  ├─ Configuration\           JSON model and ConfigStore
│  ├─ Core\                    shared scene rules
│  ├─ Removal\                 object targeting and persistence
│  ├─ Settings\                ModSettings integration
│  ├─ Shadow\                  lightmap, BC7 and D3D11 interop
│  ├─ Resources\               embedded regional BC7 patch
│  ├─ Properties\              assembly metadata
│  ├─ FarmhouseWardrobeFix.csproj
│  └─ WardrobeFixMod.cs        MelonLoader entry point
├─ Go\                          ready-to-install output
├─ build.ps1
├─ README.md
├─ README_RU.md
└─ README_EN.md
```

## Architecture

- `WardrobeFixMod` receives MelonLoader events and wires the components.
- `ConfigStore` handles JSON loading, validation and saving.
- `ObjectRemovalService` owns built-in and user-selected removals.
- `AimedObjectResolver` isolates raycasting, prefab-root selection and system
  object protection.
- `LightmapPatchService` validates the target lightmap and limits all work to
  the initial texture-loading window.
- `NativeBc7Patch` validates the embedded patch format.
- `D3D11Bc7RegionWriter` isolates unsafe low-level BC7 writes.

The mod does not modify The Long Dark save files or destroy game assets. It
only disables selected GameObjects while their scene is loaded.

## Logging

Normal operation is silent. `MelonLoader/Latest.log` contains only warnings for
invalid targets/configuration and errors from lightmap, BC7 or Direct3D 11
validation failures.
