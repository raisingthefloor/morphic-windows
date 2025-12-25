// Copyright 2020-2022 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windowsnative-lib-cs/blob/main/LICENSE
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

namespace Morphic.WindowsNative.Theme;

using Morphic.Core;

#region Legacy Morphic theme code

public static class ThemeUtils
{
    public static MorphicResult<string?, MorphicUnit> GetCurrentThemePath()
    {
        var openKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes");
        if (openKeyResult.IsError == true)
        {
            switch (openKeyResult.Error!)
            {
                case IWin32ApiError.Win32Error(var win32ErrorCode):
                    switch (win32ErrorCode)
                    {
                        case (uint)PInvoke.Win32ErrorCode.ERROR_FILE_NOT_FOUND:
                            // key was not found; return null indicating that the data does not exist
                            return MorphicResult.OkResult<string?>(null);
                        default:
                            return MorphicResult.ErrorResult();
                    }
                default:
                    return MorphicResult.ErrorResult();
            }
        }
        var themesKey = openKeyResult.Value!;

        var getValueDataResult = themesKey.GetValueData<string>("CurrentTheme");
        if (getValueDataResult.IsError == true)
        {
            switch (getValueDataResult.Error!)
            {
                case Morphic.WindowsNative.Registry.RegistryKey.IRegistryGetValueError.ValueDoesNotExist:
                    return MorphicResult.OkResult<string?>(null);
                default:
                    return MorphicResult.ErrorResult();
            }
        }
        var valueData = getValueDataResult.Value!;

        return MorphicResult.OkResult<string?>(valueData);
    }
}

#endregion Legacy Morphic theme code
