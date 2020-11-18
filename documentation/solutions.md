# morphic-windows Solutions registry

## Terminology
* *solution*: A configurable entity on the user's computer, such as an application, feature, or AT.
* *setting*: A configurable value of a solution.
* *settings handler*: A class (inheriting `SettingHandler`) which performs the reading and writing of settings.
* *setting group*: A collection of settings which are stored in the same location, using the same settings handler.
* *solution id*: Unique identifier of a solution (eg, `com.microsoft.windows.narrator`).
* *setting id*: Unique identifier of a setting, which includes the owning solution (eg, `com.microsoft.windows.narrator/enabled`).

The solutions registry is a json5 file, describing a map of solutions.

## Solution

```json5
{
  "solution-id": {
    settings: [ /* Array of setting groups */ ]
  }
}
```

## Setting groups

Solutions can have multiple setting groups. A setting group is described using the following json:

```js
settings: [{
  type: "...", // name of the settings handler.
  path: "...", // Location of the settings (such as a file, or registry key)
  settings: {
    // settings
    "setting-id": {
      name: "setting1",
      dataType: "bool",

      // Optional fields:

      local: true,
      
      range: {
        min: 0,
        max: 10,
        increment: 1
      }
    },
    settingB: {}
  }
}]
```

|Field|Description
|---|---
|`type` (required)|Name of the settings handler. See [Settings handlers](#settingshandlers) below.


### Setting

A setting group has 1 or more settings.

|Field|Description
|---|---
|`name` (required)|Name of the setting as exposed by the settings handler. For example, the registry value name.
|`dataType` (required)|`string`, `bool`, `int`, or `real`.
|`local`|Boolean to indicate if this setting is only relevant for the local machine, and will not be transferred to another. 
|`range`|The allowed range, for numeric settings. See [Range](#range) below.

### Range

Numeric settings can have a range of allowed values. The `range` is used to limit the value of the setting
within the `min` and `max` limits. The UI will also use this field to present the current value within the range.

`min` and `max` can be an absolute number. However, there are times when the range could be different and depend on,
for example, the current state or configuration of the machine. To handle this, the `min` and `max` field can also
be a setting ID, the format of `settingId [(+|-) number] [? default]`. This gives the ability to adjust it to
handle a "length vs 0-based index" value.

`increment` field is used by the UI to increase or decrease the value by a single amount. Default is `1`.

Example:
```js
range: {
  min: 0,
  max: "com.windows.display/resolutionCount - 1"
}
```

## Settings handlers

### Registry Settings Handler

Reads and writes values to and from the Windows Registry. Settings point to a given value in the registry key
specified by `path`.

```json5
{
  type: "registry",
  // Registry key path
  path: "HKCU\\Software\\Microsoft\\ScreenMagnifier",
  settings: {
    magnification: {
      // Name of the registry value
      name: "Magnification",
      dataType: "int",
      // One of REG_SZ, REG_EXPAND_SZ, REG_BINARY, REG_DWORD, REG_MULTI_SZ, or REG_QWORD
      valueKind: "REG_DWORD"
    },
    mode: {
      name: "MagnificationMode",
      dataType: "int",
      valueKind: "REG_DWORD"
    }
  }
}
```

## INI File settings handler

Reads and writes settings to and from INI files.

```json5
{
  type: "registry",
  // INI File
  path: "c:/example/settings.ini",
  settings: {
    magnification: {
      // Path to the value
      name: "group1.value1",
      dataType: "string"
    },
    mode: {
      name: "group1.subgroup.value2",
      dataType: "int"
    }
  }
}
```

Will read the following INI file:

```ini
[group1]
value1=abc
[[subgroup]]
value2=123
```

## System Settings settings handler

Gets or sets settings which are set via the System Settings app.
The [WindowsSettingTool](https://github.com/stegru/WindowsSettingsTool) can be used to establish the setting names. 

```json5
{
  type: "systemSettings",
  settings: {
    enabled: {
      name: "SystemSettings_Accessibility_ColorFiltering_IsEnabled",
      dataType: "bool"
    },
    filterType: {
      name: "SystemSettings_Accessibility_ColorFiltering_FilterType",
      dataType: "int"
    }
  }
}
```

## Process settings handler

Provides the ability to make a setting start or stop a process.

```json5
{
  type: "process",
  path: "${env:SystemRoot}/notepad.exe",
  settings: {
    enabled: {
      name: "isRunning",
      dataType: "bool"
    }
  }
}
```

Settings can access the following properties exposed by the process settings handler:

|Name|Description
|---|---
|`isRunning`|A boolean indicating whether or not the process is running.


## Display settings handler

Applies settings to the display.

Settings can access the following properties exposed by the display settings handler:

|Name|Description
|---|---
|`zoom`|Index of the available resolutions.
|`resolutionCount`|Number of resolutions available.

```json5
{
  type: "displaySettings",
  settings: {
    count: "resolutionCount", // used by zoom.count
    zoom: {
      name: "zoom",
      dataType: "int",
      local: true,
      range: {
        min: 0,
        max: "count"
      }
    }
  }
}
```
