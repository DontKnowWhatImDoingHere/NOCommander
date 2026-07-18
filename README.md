# Nuclear Option Commander

A BepInEx commander-mode mod for Nuclear Option. The mod is intended for the
host or singleplayer and reuses the game's unit commands, aircraft AI, depots,
airbases, dynamic map, faction economy, and tracking data where possible.

## Current Features

- Free commander camera toggled with `F8`
- Lightweight friendly and datalink unit markers
- 3D single selection and `Shift` multi-selection
- 3D movement orders for friendly ground vehicles and ships
- Depot spawn queues, rally points, and faction vehicle reserves
- Tactical map and draggable/scalable commander UI
- Cargo and naval supply aircraft missions
- Configurable Air Command missions and aircraft loadouts
- Radar controls and experimental mobile emplacement relocation

## Requirements

- Nuclear Option
- BepInEx 5
- Windows
- A .NET SDK with a C# compiler
- .NET Framework 4.8 or 4.8.1 reference assemblies

## Installation

Download `NuclearOptionCommander.dll` from the latest release and place it in:

```text
Nuclear Option\BepInEx\plugins\NuclearOptionCommander\
```

## Building From Source

Open `NuclearOptionCommander.csproj` with a compatible .NET SDK or IDE and pass
the Nuclear Option installation as the `GameDir` MSBuild property:

```powershell
dotnet build -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Nuclear Option"
```

`NUCLEAR_OPTION_DIR` can be set instead of passing `GameDir`. Local helper and
deployment scripts are intentionally not part of the repository.

## Notes

The mod patches runtime methods with Harmony. It does not modify or replace any
base-game files. Game updates may change private fields or methods used by the
integration and require corresponding compatibility updates.
