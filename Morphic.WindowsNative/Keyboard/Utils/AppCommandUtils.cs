// Copyright 2022 Raising the Floor - US, Inc.
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

namespace Morphic.WindowsNative.Keyboard.Utils;

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class AppCommandUtils
{
    public enum AppCommand: short
    {
        APPCOMMAND_BROWSER_BACKWARD = 1,
        APPCOMMAND_BROWSER_FORWARD = 2,
        APPCOMMAND_BROWSER_REFRESH = 3,
        APPCOMMAND_BROWSER_STOP = 4,
        APPCOMMAND_BROWSER_SEARCH = 5,
        APPCOMMAND_BROWSER_FAVORITES = 6,
        APPCOMMAND_BROWSER_HOME = 7,
        APPCOMMAND_VOLUME_MUTE = 8,
        APPCOMMAND_VOLUME_DOWN = 9,
        APPCOMMAND_VOLUME_UP = 10,
        APPCOMMAND_MEDIA_NEXTTRACK = 11,
        APPCOMMAND_MEDIA_PREVIOUSTRACK = 12,
        APPCOMMAND_MEDIA_STOP = 13,
        APPCOMMAND_MEDIA_PLAY_PAUSE = 14,
        APPCOMMAND_LAUNCH_MAIL = 15,
        APPCOMMAND_LAUNCH_MEDIA_SELECT = 16,
        APPCOMMAND_LAUNCH_APP1 = 17,
        APPCOMMAND_LAUNCH_APP2 = 18,
        APPCOMMAND_BASS_DOWN = 19,
        APPCOMMAND_BASS_BOOST = 20,
        APPCOMMAND_BASS_UP = 21,
        APPCOMMAND_TREBLE_DOWN = 22,
        APPCOMMAND_TREBLE_UP = 23,
        APPCOMMAND_MICROPHONE_VOLUME_MUTE = 24,
        APPCOMMAND_MICROPHONE_VOLUME_DOWN = 25,
        APPCOMMAND_MICROPHONE_VOLUME_UP = 26,
        APPCOMMAND_HELP = 27,
        APPCOMMAND_FIND = 28,
        APPCOMMAND_NEW = 29,
        APPCOMMAND_OPEN = 30,
        APPCOMMAND_CLOSE = 31,
        APPCOMMAND_SAVE = 32,
        APPCOMMAND_PRINT = 33,
        APPCOMMAND_UNDO = 34,
        APPCOMMAND_REDO = 35,
        APPCOMMAND_COPY = 36,
        APPCOMMAND_CUT = 37,
        APPCOMMAND_PASTE = 38,
        APPCOMMAND_REPLY_TO_MAIL = 39,
        APPCOMMAND_FORWARD_MAIL = 40,
        APPCOMMAND_SEND_MAIL = 41,
        APPCOMMAND_SPELL_CHECK = 42,
        APPCOMMAND_DICTATE_OR_COMMAND_CONTROL_TOGGLE = 43,
        APPCOMMAND_MIC_ON_OFF_TOGGLE = 44,
        APPCOMMAND_CORRECTION_LIST = 45,
        APPCOMMAND_MEDIA_PLAY = 46,
        APPCOMMAND_MEDIA_PAUSE = 47,
        APPCOMMAND_MEDIA_RECORD = 48,
        APPCOMMAND_MEDIA_FAST_FORWARD = 49,
        APPCOMMAND_MEDIA_REWIND = 50,
        APPCOMMAND_MEDIA_CHANNEL_UP = 51,
        APPCOMMAND_MEDIA_CHANNEL_DOWN = 52,
        APPCOMMAND_DELETE = 53,
        APPCOMMAND_DWM_FLIP3D = 54,
    }

    // NOTE: this overload is provided in case the caller doesn't have a window to send the application command from; note that this scenario isn't quite as fail-proof as providing an hwnd (since explorer.exe could be crashed, restarting, etc.)
    public static MorphicResult<MorphicUnit, Win32ApiError> GenerateApplicationCommandEvent(AppCommand appCommand)
    {
        var taskbarHwnd = PInvoke.User32.FindWindow("Shell_TrayWnd", null);
        if (taskbarHwnd == IntPtr.Zero)
        {
            // could not find taskbar window
            var win32ErrorCode = PInvoke.Kernel32.GetLastError();
            return MorphicResult.ErrorResult(Win32ApiError.Win32Error((uint)win32ErrorCode));
        }

        // NOTE: many implementations of SendMessage(...WM_APPCOMMAND) use a sourceHwnd of IntPtr.Zero; we've chosen to use the taskbar as both source and target hwnd out of an abundance of caution; if this doesn't work well in practice, try following the example of others by setting the sourceHwnd to IntPtr.Zero
        return AppCommandUtils.GenerateApplicationCommandEvent(taskbarHwnd, taskbarHwnd, appCommand);
    }

    public static MorphicResult<MorphicUnit, Win32ApiError> GenerateApplicationCommandEvent(IntPtr sourceHwnd, IntPtr targetHwnd, AppCommand appCommand)
    {
        var uDevice = ExtendedPInvoke.FAPPCOMMAND_KEY; // "user pressed a key" (i.e. we are generating an app command as a virtual key press)
        ushort dwKeys = 0; // no virtual mouse/modifier/X keys are down

        return AppCommandUtils.GenerateApplicationCommandEvent(sourceHwnd, targetHwnd, dwKeys, uDevice, appCommand);
    }

    public static MorphicResult<MorphicUnit, Win32ApiError> GenerateApplicationCommandEvent(IntPtr sourceHwnd, IntPtr targetHwnd, ushort uDevice, ushort dwKeys, AppCommand appCommand)
    {
        if ((uDevice & 0xF000) != uDevice)
        {
            throw new ArgumentOutOfRangeException(nameof(uDevice));
        }

        IntPtr wParam = sourceHwnd;
        //
        var cmd = (ushort)appCommand & 0x0FFF;
        IntPtr lParam = (IntPtr)(((cmd | uDevice) << 16) | dwKeys);

        // NOTE: Microsoft does not seem to provide clear documentation on the successful result from SendMessage when sending WM_APPCOMMAND; the following error condition is based on observation only (which is why we use debug.assert instead of returning an error);
        //       [as we expand our use of WM_APPCOMMAND beyond our initial use case of fire-and-forget system commands such as volume up/down/mute, we should revisit this; sending commands like COPY/PASTE to a window may actually return a different result, based on APPCOMMAND]
        var sendMessageResult = PInvoke.User32.SendMessage(targetHwnd, PInvoke.User32.WindowMessage.WM_APPCOMMAND, wParam, lParam);
        if (sendMessageResult != IntPtr.Zero)
        {
            // unexpected error
            var win32ErrorCode = PInvoke.Kernel32.GetLastError();
            Debug.Assert(false, "SendMessage WM_APPCOMMAND returned an unexpected result; win32 error code is: " + win32ErrorCode.ToString());
        }

        return MorphicResult.OkResult();
    }
}
