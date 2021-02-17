# Morphic Windows Client

[https://morphic.org/](https://morphic.org/)

## Building Locally

The client and its dependent projects can be built using MSBuild:

    msbuild Morphic.Client

To build the installer (add `-p:Configuration=Release` as required):

    msbuild Morphic.Setup

## Running (development)

Use the following launch profile:

* Morphic

### Configuration

These launch profiles will use [`appsettings.Debug.json`](Morphic.Client/appsettings.Debug.json)
(and [`appsettings.Local.json`](Morphic.Client/appsettings.Local.json) if it exists). The addresses for the API and
Web servers are defined in these files, in the `MorphicService` section. Developer-specific changes should be put
in `appsettings.Local.json`.

## Documentation

* [Configuration](documentation/configuration.md)
* [Solutions Registry](documentation/solutions.md)
* [Bar JSON format](Morphic.Client/Bar/BarData.md)
