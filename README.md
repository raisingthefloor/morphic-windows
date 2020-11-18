# Morphic Windows Client

[https://morphic.org/](https://morphic.org/)

## Building Locally

The client, and its dependent projects, can be built using MSBuild:

    msbuild Morphic.Client

To build the installers (add `-p:Configuration=Release` as required):

    msbuild Morphic.Setup -p:Edition=Basic
    msbuild Morphic.Setup -p:Edition=Community

## Documentation

* [Configuration](documentation/configuration.md)
* [Solutions Registry](documentation/solutions.md)
* [Bar JSON format](Morphic.Client/Bar/BarData.md)
