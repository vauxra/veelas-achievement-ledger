# Local Development Environment

## Project

- Project root: `/mnt/mintData/git/achievement-tracker-mod`

## FFXIV / XIVLauncher / Dalamud paths

Known local paths:

- FFXIV/XIVLauncher game files: `/mintData/games/.xlcore/ffxiv`
- Same location via mounted path: `/mnt/mintData/games/.xlcore/ffxiv`
- Dalamud directory: `/mintData/games/.xlcore/dalamud`
- Dalamud assets directory: `/mintData/games/.xlcore/dalamudAssets`
- Installed plugins directory: `/mintData/games/.xlcore/installedPlugins`
- Plugin configs directory: `/mintData/games/.xlcore/pluginConfigs`

The originally reported config path `/mintData/games/.xlcore/ffxiv/config` was not present during checks. Use the discovered `.xlcore/pluginConfigs` path for plugin config investigation unless XIVLauncher exposes a different dev-plugin location in-game.

## .NET SDK

System dotnet initially had runtime 10.0.8 but no SDK. Because sudo requires a password, the SDK was installed user-local with Microsoft's dotnet-install script:

- User-local dotnet: `/home/developer/.dotnet/dotnet`
- Installed SDK: `10.0.300`

Use this dotnet explicitly in commands or export PATH:

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
```

Verify:

```bash
$HOME/.dotnet/dotnet --info
$HOME/.dotnet/dotnet --list-sdks
```

## SamplePlugin status at scaffold time

The official SamplePlugin was checked from:

- `https://github.com/goatcorp/SamplePlugin`

Observed:

- README still says `.NET Core 8 SDK` prerequisite.
- `SamplePlugin/SamplePlugin.csproj` uses `Dalamud.NET.Sdk/15.0.0`.
- `SamplePlugin/packages.lock.json` targets `net10.0-windows7.0`.

For this project, build with the user-local .NET 10 SDK unless the official template/docs change.
