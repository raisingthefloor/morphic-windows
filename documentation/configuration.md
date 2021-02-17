# morphic-windows configuration

## User configuration

Configuration which gets updated by the application is stored in the registry path
`HKEY_CURRENT_USER\Software\Raising the Floor\Morphic`. This is centrally handled by the
[`AppOptions`](../Morphic.Client/Config/AppOptions.cs) class.

## Launch Options

`morphic.exe` supports the following command-line options and environment variables, which are used during
development in the [`launchSettings.json`](../Morphic.Client/Properties/launchSettings.json) file.

|Option|Environment|Description
|---|---|---
|`--debug`|`MORPHIC_DEBUG=1`|Run in "debug mode" (the files `appsettings.Debug.json` and `appsettings.Local.json` are used for configuration)
|`--bar BARFILE`|`MORPHIC_BAR=BARFILE`|Use `BARFILE` as the json5 file to load as the initial bar

## Static configuration

The following files are located next to the application executable (usually in `C:\Program Files\Morphic`).
The application does not write to these.

### Bar configuration

* [`DefaultConfig/default-bar.json5`](../Morphic.Client/DefaultConfig/default-bar.json5):
Contains the base bar information on which other bars (Basic MorphicBar or downloaded custom bars) are loaded.

* [`DefaultConfig/basic-bar.json5`](../Morphic.Client/DefaultConfig/basic-bar.json5):
The bar definition for the Basic MorphicBar.

* [`DefaultConfig/presets.json5`](../Morphic.Client/DefaultConfig/presets.json5):
Definitions for various setting and action bar items. See [BarData](../Morphic.Client/Bar/BarData.md#presetsjson5)

### Solutions Registry

[`solutions.json`](../Morphic.Client/solutions.json5)

### Build information

* [`build-info.json`](../Morphic.Client/build-info.json):
 Created during in the build pipeline, by [`set-build-info.sh`](../set-build-info.sh), containing the release version.
