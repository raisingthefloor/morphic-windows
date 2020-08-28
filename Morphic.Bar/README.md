# Morphic Community Bar

Displays a bar containing buttons, whose items are defined in a web app.

## Build

    dotnet build

## Make the installer

    Setup\build.bat

(requires Inno Setup)

## Adding buttons

Simple buttons (web links and most applications) can be defined by the web app. Anything beyond that will need to be
added to [`DefaultConfig/presets.json5`](DefaultConfig/presets.json5). [More information](BarData.md#actionsjson5).




## Configuration

See [BarData.md](BarData.md).

The base bar configuration is defined in [`default-bar.json5`](DefaultConfig/default-bar.json5).

Default configuration is read from `./DefaultConfig`. Can be overridden by files of the same name in the config directory,
`%LOCALAPPDATA%\MorphicCommunity`.

### Command-line arguments

|Name|Description|
|---|---|
|`--bar FILE`|Loads `FILE`, which contains the bar json data.|

### Environment variables

|Name|Description|
|---|---|
|`MORPHIC_LOGFILE`|Name of the log file. (a number will be added to this file if required for rotation)|
|`MORPHIC_LOGLEVEL`|Lowest level item to log (see [LogLevel Enum](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel?view=dotnet-plat-ext-3.1)).|
|`MORPHIC_CONFIGDIR`|Directory to use for configuration/logging/cache (default: `%LOCALAPPDATA%\MorphicCommunity`)|

## Logging

Look in `%LOCALAPPDATA%\MorphicCommunity`.

Loglevel (`%MORPHIC_LOGLEVEL%`) of `debug` or lower also writes to the console.
