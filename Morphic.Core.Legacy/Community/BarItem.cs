// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under 
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and 
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants 
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant 
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Morphic.Core.Legacy.Community;

public class BarItem
{
    [JsonPropertyName("kind")]
    public BarItemKind Kind { get; set; }

    [JsonPropertyName("is_primary")]
    public bool IsPrimary { get; set; } = false;

    [JsonPropertyName("configuration")]
    public Dictionary<string, object?>? Configuration { get; set; }

    [JsonIgnore]
    public string? ButtonLabel
    {
        get
        {
            object? value = null;
            if (Configuration?.TryGetValue("label", out value) ?? false)
            {
                if (value is string stringValue)
                {
                    return stringValue;
                }
            }
            return null;
        }
    }

    [JsonIgnore]
    public Uri? ButtonImageUri
    {
        get
        {
            object? value = null;
            if (Configuration?.TryGetValue("image_url", out value) ?? false)
            {
                if (value is string stringValue)
                {
                    try
                    {
                        if (stringValue.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || stringValue.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            return new Uri(stringValue);
                        }
                        return new Uri(stringValue, UriKind.Relative);
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            return null;
        }
    }

    [JsonIgnore]
    public string? DefaultApplicationName
    {
        get
        {
            object? value = null;
            if (Configuration?.TryGetValue("default", out value) ?? false)
            {
                if (value is string stringValue)
                {
                    return stringValue;
                }
            }
            return null;
        }
    }

    [JsonIgnore]
    public string? ExeName
    {
        get
        {
            object? value = null;
            if (Configuration?.TryGetValue("exe", out value) ?? false)
            {
                if (value is string stringValue)
                {
                    return stringValue;
                }
            }
            return null;
        }
    }

    [JsonIgnore]
    public string? ActionIdentifier
    {
        get
        {
            object? value = null;
            if (Configuration?.TryGetValue("identifier", out value) ?? false)
            {
                if (value is string stringValue)
                {
                    return stringValue;
                }
            }
            return null;
        }
    }

    [JsonIgnore]
    public string? ColorHexString
    {
        get
        {
            object? value = null;
            if (Configuration?.TryGetValue("color", out value) ?? false)
            {
                if (value is string stringValue)
                {
                    return stringValue;
                }
            }
            return null;
        }
    }

    [JsonIgnore]
    public Uri? LinkUri
    {
        get
        {
            object? value = null;
            if (Configuration?.TryGetValue("url", out value) ?? false)
            {
                if (value is string stringValue)
                {
                    try
                    {
                        return new Uri(stringValue);
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            return null;
        }
    }
}

public enum BarItemKind
{
    Link,
    Application,
    Action
}
