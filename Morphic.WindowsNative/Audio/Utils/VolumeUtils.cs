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

namespace Morphic.WindowsNative.Audio.Utils;

using Morphic.Core;
using System;

public class VolumeUtils
{
    public static MorphicResult<MorphicUnit, Win32ApiError> SendVolumeDownCommand(uint percent)
    {
        // if no sourceHwnd was provided, then use IntPtr.Zero; we'll consider this to be "null" and will send the command from the taskbar instead
        return VolumeUtils.SendVolumeDownCommand(IntPtr.Zero, percent);
    }
    //
    public static MorphicResult<MorphicUnit, Win32ApiError> SendVolumeDownCommand(IntPtr sourceHwnd, uint percent)
    {
        if (percent % 2 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), "Argument must be an even value, as each key reduces the volume % by 2");
        }
        if (percent > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), "Argument may not exceed 100 percent");
        }

        var keyPressCount = percent / 2;
        
        for (var i = 0; i < keyPressCount; i += 1)
        {
            var generateAppCommandResult = VolumeUtils.InternalSendAppCommand(sourceHwnd, Keyboard.Utils.AppCommandUtils.AppCommand.APPCOMMAND_VOLUME_DOWN);
            if (generateAppCommandResult.IsError == true)
            {
                return MorphicResult.ErrorResult(generateAppCommandResult.Error!);
            }
        }

        return MorphicResult.OkResult();
    }

    public static MorphicResult<MorphicUnit, Win32ApiError> SendVolumeUpCommand(uint percent)
    {
        // if no sourceHwnd was provided, then use IntPtr.Zero; we'll consider this to be "null" and will send the command from the taskbar instead
        return VolumeUtils.SendVolumeUpCommand(IntPtr.Zero, percent);
    }
    //
    public static MorphicResult<MorphicUnit, Win32ApiError> SendVolumeUpCommand(IntPtr sourceHwnd, uint percent)
    {
        if (percent % 2 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), "Argument must be an even value, as each key reduces the volume % by 2");
        }
        if (percent > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), "Argument may not exceed 100 percent");
        }

        var keyPressCount = percent / 2;

        for (var i = 0; i < keyPressCount; i += 1)
        {
            var generateAppCommandResult = VolumeUtils.InternalSendAppCommand(sourceHwnd, Keyboard.Utils.AppCommandUtils.AppCommand.APPCOMMAND_VOLUME_UP);
            if (generateAppCommandResult.IsError == true)
            {
                return MorphicResult.ErrorResult(generateAppCommandResult.Error!);
            }
        }

        return MorphicResult.OkResult();
    }

    public static MorphicResult<MorphicUnit, Win32ApiError> SendVolumeMuteToggleCommand()
    {
        // if no sourceHwnd was provided, then use IntPtr.Zero; we'll consider this to be "null" and will send the command from the taskbar instead
        return VolumeUtils.SendVolumeMuteToggleCommand(IntPtr.Zero);
    }
    //
    public static MorphicResult<MorphicUnit, Win32ApiError> SendVolumeMuteToggleCommand(IntPtr sourceHwnd)
    {
        var generateAppCommandResult = VolumeUtils.InternalSendAppCommand(sourceHwnd, Keyboard.Utils.AppCommandUtils.AppCommand.APPCOMMAND_VOLUME_MUTE);
        if (generateAppCommandResult.IsError == true)
        {
            return MorphicResult.ErrorResult(generateAppCommandResult.Error!);
        }

        return MorphicResult.OkResult();
    }

    /* helper functions */

    private static MorphicResult<MorphicUnit, Win32ApiError> InternalSendAppCommand(IntPtr sourceHwnd, Keyboard.Utils.AppCommandUtils.AppCommand appCommand)
    {
        MorphicResult<MorphicUnit, Win32ApiError> generateAppCommandResult;
        if (sourceHwnd != IntPtr.Zero)
        {
            generateAppCommandResult = Morphic.WindowsNative.Keyboard.Utils.AppCommandUtils.GenerateApplicationCommandEvent(sourceHwnd, sourceHwnd, appCommand);
        }
        else
        {
            generateAppCommandResult = Morphic.WindowsNative.Keyboard.Utils.AppCommandUtils.GenerateApplicationCommandEvent(appCommand);
        }
        //
        if (generateAppCommandResult.IsError == true)
        {
            return MorphicResult.ErrorResult(generateAppCommandResult.Error!);
        }

        return MorphicResult.OkResult();
    }

}
