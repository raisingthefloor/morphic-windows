# Morphic Windows Client

[https://morphic.org/](https://morphic.org/)

## Building Locally

The client, and its dependent projects, can be built using MSBuild:

    msbuild Morphic.Client

To build the installers (add `-p:Configuration=Release` as required):

    msbuild Morphic.Setup -p:Edition=Basic
    msbuild Morphic.Setup -p:Edition=Community

## Running (development)

One of two launch profiles can be used, depending on the edition:

* Morphic-Basic
* Morphic-Community

### Configuration

These launch profiles will use [`appsettings.Debug.json`](Morphic.Client/appsettings.Debug.json)
(and [`appsettings.Local.json`](Morphic.Client/appsettings.Local.json) if it exists). The addresses for the API and
Web servers are defined in these files, in the `MorphicService` section. Developer-specific changes should be put
in `appsettings.Local.json`.

## Documentation

* [Configuration](documentation/configuration.md)
* [Solutions Registry](documentation/solutions.md)
* [Bar JSON format](Morphic.Client/Bar/BarData.md)
