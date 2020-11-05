# The INI file reader/writer.

Reads and writes INI files.

The main goal of this library is to only make small specific changes to an existing INI file, while preserving the
formatting of the rest of the file.

This is a C# port of [gpii-ini-file](https://github.com/GPII/universal/blob/master/gpii/node_modules/gpii-ini-file/README.md).

## INI file format

INI file can look something like this:

```ini
# comments
; other comments

# = or : delimiters.
key=value1
key2:value2
key3 = value3

# Sections (and sub-sections)
[section]
key="section-value"

[[subsection]]
key=subsection value

[section2]
# Multiple lines
multi-line="""line1
line2
line3"""
multi-line2=line1
    line2
    line3
```

Which will parse to the following dictionary:


|key|value|
|---|---|
|`key`|`value1`|
|`key2`|`value2`|
|`key3`|`value3`|
|`section.key`|`section-value`|
|`section.subsection.key`|`subsection`|`value`|
|`section2.multi-line`|`line1\nline2\nline3`|
|`section2.multi-line2`|`line1\nline2\nline3`|


See [read-test.ini](https://github.com/GPII/universal/blob/master/gpii/node_modules/gpii-ini-file/test/read-test.ini)
for an extreme example.

When writing to an existing file, the content is modified rather than re-writing. Effort is made to respect the
unchanged values and current format of the file. An updated version of the above example will look like this:

```ini
# comments
; other comments

# = or : delimiters.
key=new value
key2:another new value
key3 = and again

# Sections
[section]
key="quotes respected"
# sub-sections
[[subsection]]
key=subsection value

new1=new value
[section2]
# Multiple lines
multi-line="""modified line1
modified line2
modified line3"""
multi-line2=modified line1
    modified line2
    modified line3

[newSection]
newValue=value
```

## How it works

Parsing is performed by a single regular expression (see [IniFileRegex.cs](IniFileRegex.cs)), which calls a function when it matches
either a section header (`[example]`), or a `key=value` pair.

When reading data, it fills an object with the matched values.

When writing, it will replace the matches with text from an existing object. Only the text in the value is replaced, and
only if it is different to the stored value.

