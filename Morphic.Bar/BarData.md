# Data structure for Morphic Bar

## `Bar`

This is what the client can handle for the bar, which is a super-set of what is provided by the web app.

There is initial data, in `default-bar.json5`. This is loaded first, then the data from the web app is merged over it.

Not all fields are required, as the client will already have its own predefined defaults. Assume fields to be optional, unless stated.

```js
Bar = {
  // Bar identifier
  id: "bar1",
  // Bar name
  name: "Example bar",

  // Initial position
  position: {
      // Dock it to an edge, reserving desktop space
      docked: "left", // optional. "left", "right", "top", "bottom", or "none" (default)

      // Position of the bar. Can be "Left"/"Top", "Middle", "Right"/"Bottom", a number, or a percentage.
      // Numbers or percentages can be negative (including -0), meaning distance from the right.
      // Percentages specify the position of the middle of the bar.
      // Ignored if `docked` is used.
      x: "50%",
      y: "Bottom",

      // Position of the secondary bar, relative to the primary bar. Same syntax as `x`/`y` above.
      // (can be split with `secondaryX` and `secondaryY`)
      secondary: "Middle",

      // Position of the expander button (the thing that opens the secondary bar)
      // (can be split with `expanderX` and `expanderY`)
      expander: "Middle",
      // What the position in `expander` is relative to.
      // "primary", "secondary", or "both" (secondary if the secondary bar is open, otherwise primary)
      expanderRelative: "Both",
  },

  // Settings for the secondary bar
  secondaryBar: {
    // Close the secondary bar when another application takes focus.
    autohide: true,
    // Hide the expander button when another application takes focus (shown on mouse-over).
    autohideExpander: false
  },

  // Size of everything
  scale: 1,

  // What happens when all buttons do not fit.
  //  "resize": shrinks some items until it fits
  //  "wrap": Adds another column
  //  "hide": Do nothing.
  overflow: "resize",

  // Bar theme
  theme: {Theme},

  // Theme for bar items
  itemTheme: {ItemTheme},

  // The bar items
  items: [
    {BarItem}
  ]
}
```

## `BarItem`

Describes an individual bar item.

```js
BarItem = {
  // email|calendar|videocall|photos|...
  // Probably be ignored by the client
  category: "calendar",

  // unique identifier (probably ignored by client)
  id: "calendar-button",

  // `true` if the item is shown on the primary bar. `false` to show on the secondary bar.
  is_primary: true,

  // Per-button theme, overrides the `Bar.itemTheme` field from above.
  // If unset, this is generated using `configuration.color`
  theme: {Theme},

  // `true` to not show this button. While it's expected that the client will only receive the items which should be
  // shown, this field provides the ability to show or hide items depending on the platform, using the platform
  // identifier, described later. For example, `hidden$win: true` will make the item only available for macOS.
  hidden: false,

  // Items are sorted by this (higher values at the top).
  priority: 0,

  // The kind of item (see  Item kinds below) [REQUIRED]
  // "link", "application", "action"
  kind: "link",

  // Specific to the item kind.
  configuration: {
    // ...
  }
}
```

## Button items

Button-like items: "link", "application", "internal", "action"

```js
/** @mixes BarItem */
ButtonItem = {
  kind: "<link|application|internal|action>",
  configuration: {
    // Displayed on the button [REQUIRED]
    label: "Calendar",

    // Tooltip.
    toolTipHeader: "Open the calendar",
    // More details.
    toolTipInfo: "Displays your google calendar",

    // local/remote url of the icon. For values without a directory, a matching file in ./Assets/bar-icons/`) is
    // discovered. If this value is omitted (or not working), an image is detected depending on the kind of item:
    // - link: favicon of the site.
    // - application: The application icon.
    image_url: "calendar.png",

    // Item color (overrides BarItem.theme, generates the different shades for the states)
    color: '#002957',

    // Size of the item. "textonly", "small", "medium", or "large" (default)
    size: "large",
  }
}
```

### `kind = "link"`

Opens a web page. See [LinkAction](#LinkAction).

```js
/** @extends ButtonItem */
LinkButton = {
  kind: "link",
  /** @mixes LinkAction */
  configuration: {}
}
```

### `kind = "application"`

Opens an application. See [ApplicationAction](#ApplicationAction)

```js
/** @extends ButtonItem */
ApplicationButton = {
  kind: "application",
  /** @mixes ApplicationAction */
  configuration: {}
}
```

### `kind = "internal"`

Invokes a built-in routine. See [InternalAction](#InternalAction).

```js
/** @extends ButtonItem */
InternalButton = {
  kind: "internal",
  /** @mixes InternalAction */
  configuration: {}
}
```

### `kind = "action"`

Invokes a pre-defined action in [`actions.json5`](#actions.json5). See [PresetAction](#PresetAction).

```js
/** @extends ButtonItem */
ActionButton = {
  kind: "action",
  /** @mixes PresetAction */
  configuration: {}
}
```

## `Theme`

Used to specify the theme of the bar or an item.

```js
Theme = {
  color: "white",
  background: "#002957",
  // Only used by bar items
  borderColor: "#ff0",
  borderOuterColor: "#000",
  borderSize: 2
}
```


## `ItemTheme : Theme`

```js
/** @extends Theme */
ItemTheme = {
  // from Theme
  color: "white",
  background: "#002957",
  borderColor: "#ff0",
  borderOuterColor: "#000",
  borderSize: 2,

  // Optional, will use the above style.
  hover: {Theme}, // Mouse is over the item.
  focus: {Theme}, // Item has keyboard focus.
  active: {Theme} // Item is being clicked (mouse is down).
}
```

## actions.json5

This file contains the definitions actions performed by buttons with `kind = "action"`. The value of `ButtonItem.identifier` is a key in `actions`.

```js
actions.json5 = {
  actions: {
    "identifier": {Action},

    // Example: start task manager
    "taskManager": {
      kind: "application",
      exe: "taskmgr.exe"
    },

    // Example: invoke an internal function
    "example": {
      kind: "internal",
      function: "hello"
    }
  }
}
```

```js
Action = {
  kind: "<link|application|internal|action>",
  // kind-specific values ...
}
```

### LinkAction

Opens a web page

```js
/** @extends Action */
LinkAction = {
  kind: "link",
  url: "https://example.com"
}
```

### ApplicationAction

Opens an application

```js
/** @extends Action */
ApplicationAction = {
  kind: "application",
  // Executable name (or full path). Full path is discovered via `App Paths` registry or the PATH environment variable.
  exe: "notepad.exe"
}
```

### InternalAction

Invokes a built-in routine.

```js
/** @extends Action */
InternalAction = {
  kind: "internal",
  // Name of the internal function.
  function: "fname",
  // Arguments to pass.
  args: ["a1", "a2"]
}
```

### PresetAction

Runs a pre-defined action in `actions.json5`

```js
/** @extends Action */
PresetAction = {
  kind: "action",
  // The key to the item in actions.json5
  identifier: "id"
}
```

## Cross-platform

All fields in the bar json and `actions.json5` can be suffixed with an OS identifier (`$mac` or `$win`), which will take precedence over the non-suffixed field. This pre-processing would be done on the client.

examples:

```js
[
  {
    command: "default command",
    command$mac: "macOS command",

    label$win: "on windows",
    labelText: "not windows"
  },
  {
    command: "default command",
    command$win: "windows command"
  },
  {
    command: "default command (ignored)",
    command$win: "windows command",
    command$mac: "macOS command"
  },
]
```
