// Copyright 2020-2024 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/master/LICENSE.txt
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

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Client.Utils;

internal class WebBrowserUtils
{
    public static MorphicResult<MorphicUnit, MorphicUnit> OpenBrowserToUri(string uriAsString)
    {
        var convertUriResult = WebBrowserUtils.ConvertStringToUri(uriAsString);
        if (convertUriResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var uri = convertUriResult.Value!;

        return WebBrowserUtils.OpenBrowserToUri(uri);

    }
    //
    public static MorphicResult<MorphicUnit, MorphicUnit> OpenBrowserToUri(Uri uri)
    {
        // for safety, make sure that the URI scheme is allowed by this application
        if (WebBrowserUtils.IsUriAllowedToOpen(uri) == false)
        {
            return MorphicResult.ErrorResult();
        }

        Process? process = Process.Start(
            new ProcessStartInfo()
            {
                FileName = uri.ToString(),
                UseShellExecute = true
            });
        //
        if (process is not null)
        {
            return MorphicResult.OkResult();
        }
        else
        {
            return MorphicResult.ErrorResult();
        }
    }

    public static MorphicResult<Uri, MorphicUnit> ConvertStringToUri(string uriAsString)
    {
        Uri? uri;
        var createResult = Uri.TryCreate(uriAsString, UriKind.Absolute, out uri);
        if (createResult == false || uri is null)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult(uri);
    }

    public static bool IsUriAllowedToOpen(Uri uri)
    {
        // NOTE: for safety, we only allow certain types of URIs to open using this application
        switch (uri.Scheme.ToLowerInvariant())
        {
            case "http":
            case "https":
                // web resource; allowed
                return true;
            //case "skype":
            //    // TODO: in the future, we may want to only open Skype URLs by launching Skype directly
            //    // allowed
            //    return true;
            default:
                // other uri schemes are not allowed
                return false;
        }

    }

}
