// Copyright 2025 Raising the Floor - US, Inc.
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

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.Keyboard;

public class Keyboard
{
    // NOTE: this list must remain in sync with the s_allAccessoryVirtualKeys array (below)
    public enum AccessoryKey
    {
        //Alt,
        LeftAlt,
        RightAlt,
        //
        CapsLock,
        //
        Control,
        LeftControl,
        RightControl,
        //
        Shift,
        LeftShift,
        RightShift,
        //
        LeftWindows,
        RightWindows,
    }
    //
    // NOTE: this list must remain in sync with the AccessoryKey enumeration (above)
    private static readonly Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY[] s_allAccessoryVirtualKeys = [
        // Alt key
        //Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_MENU,
        Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_LMENU,
        Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_RMENU,
        //
        // Caps Lock key
        Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_CAPITAL,
        //
        // Control key
        //Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_CONTROL,
        Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_LCONTROL,
        Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_RCONTROL,
        //
        // Shift key
        //Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_SHIFT,
        Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_LSHIFT,
        Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_RSHIFT,
        //
        // Windows key
        Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_LWIN,
        Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_RWIN,
    ];

    public interface ISynthesizeKeyInputError
    {
        public record KeyboardNotIdle : ISynthesizeKeyInputError;
        public record Win32Error(uint win32ErrorCode) : ISynthesizeKeyInputError;
    }
    // NOTE: this function simulates the pressing of accessory keys and then the designated key (i.e. inserting them into the keyboard input stream);
    //       it then releases those keys in the reverse order;
    //       it does this as one API call, avoiding any key repeat events.
    // NOTE: if the keyboard currently has keys depressed, this function will fail (as the keys are applied on top of the current physical state of the keyboard)
    // NOTE: this function may be used by accessibility apps to synthesize and insert system-wise keyboard shortcuts (to be handled by the shell); such behavior from non-accessibility-permissioned apps is not guaranteed
    // NOTE: synthesized key events cannot be inserted into the key stream for receipt by apps with higher security permissions
    public static MorphicResult<MorphicUnit, ISynthesizeKeyInputError> SynthesizeKeyPress(List<AccessoryKey> accessoryKeys, char? key)
    {
        if (key is not null)
        {
            // verify that the provided character key is valid
            // NOTE: we may also want to provide options for other virtual keys (which are not simply numbers of letters); to do so, we would ideally expose our own virtual keys enumeration
            // see: https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
            if ((key >= '0' && key <= '9') ||
                (key >= 'A' && key <= 'Z'))
            {
                // allowed char (which matches vkey code via simple cast)
            }
            else if (s_allAccessoryVirtualKeys.Contains((Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY)key.Value)) 
            {
                // virtual accessory keys are definitely not allowed as the 'key' argument
                throw new ArgumentOutOfRangeException(nameof(key), "Invalid keycode; accessory keys must be specified in the '" + nameof(accessoryKeys) + "' argument");
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(key), "Invalid keycode; must be in the digit char range '0'..='9' or uppercase char range 'A'..='Z'");
            }

        }

        const short SHORT_HIGH_BIT = unchecked((short)0x8000);

        List<Windows.Win32.UI.Input.KeyboardAndMouse.INPUT> inputs = [];

        // NOTE: in early testing, this value just returned zero.  As Microsoft's sample does not include this requirement, we have omitted it--but if we need to include it we can uncomment this line (and include the value in the INPUT struct instances below)
//        var currentThreadMessageExtraInfo = (nuint)Windows.Win32.PInvoke.GetMessageExtraInfo().Value;

        // step 1: convert all accessory keys to virtual keys (i.e. the list of keys that need to be pressed)
        List<Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY> accessoryVirtualKeysToPress = new(accessoryKeys.Count);
        foreach (var accessoryKey in accessoryKeys)
        {
            var vKey = Keyboard.ConvertAccessoryKeyToWin32Vkey(accessoryKey) ?? throw new ArgumentOutOfRangeException("Argument '" + nameof(accessoryKeys) + "' contains unknown accessory key");
            accessoryVirtualKeysToPress.Add(vKey);
        }

        // step 2: get the current (active) key state of all the accessory virtual keys
        var allAccessoryVirtualKeysStates = GetAllAccessoryVirtualKeysStates();

        // step 3: if any accessory keys are pressed which are in our list, remove them from our list of keys to press;
        //         if any accessory keys are pressed which are not in our desired list, return an error
        foreach (var vKey in allAccessoryVirtualKeysStates.Keys)
        {
            if ((allAccessoryVirtualKeysStates[vKey] & SHORT_HIGH_BIT) != 0)
            {
                if (accessoryVirtualKeysToPress.Contains(vKey) == true)
                {
                    // if this key is meant to be pressed, then remove it from the list (since it's already pressed)
                    accessoryVirtualKeysToPress.Remove(vKey);
                }
                else
                {
                    // if this key is not meant to be pressed, abort (since it conflicts with our accessory keys)
                    return MorphicResult.ErrorResult<ISynthesizeKeyInputError>(new ISynthesizeKeyInputError.KeyboardNotIdle());
                }
            }
        }

        // step 4: add keydowns for the accessory keys
        foreach (var accessoryVirtualKeyToPress in accessoryVirtualKeysToPress)
        {
            var input = new Windows.Win32.UI.Input.KeyboardAndMouse.INPUT();
            input.Anonymous.ki = new Windows.Win32.UI.Input.KeyboardAndMouse.KEYBDINPUT()
            {
                wVk = accessoryVirtualKeyToPress,
                //dwFlags = 0, // [the default, presumably means 'KEYDOWN']
//                dwExtraInfo = currentThreadMessageExtraInfo,
            };
            input.type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD;
            inputs.Add(input);
        }

        // step 5: add a keydown for the non-accessory key
        if (key is not null)
        {
            var input = new Windows.Win32.UI.Input.KeyboardAndMouse.INPUT();
            input.Anonymous.ki = new Windows.Win32.UI.Input.KeyboardAndMouse.KEYBDINPUT()
            {
                wVk = (Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY)key.Value,
                //dwFlags = 0, // [the default, presumably means 'KEYDOWN']
//                dwExtraInfo = currentThreadMessageExtraInfo,
            };
            input.type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD;
            inputs.Add(input);
        }

        // step 6: add a keyup for the non-accessory key
        if (key is not null)
        {
            var input = new Windows.Win32.UI.Input.KeyboardAndMouse.INPUT();
            input.Anonymous.ki = new Windows.Win32.UI.Input.KeyboardAndMouse.KEYBDINPUT()
            {
                wVk = (Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY)key.Value,
                dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
//                dwExtraInfo = currentThreadMessageExtraInfo,
            };
            input.type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD;
            inputs.Add(input);
        }

        // step 7: add keyups for the accessory keys
        foreach (var accessoryVirtualKeyToPress in accessoryVirtualKeysToPress)
        {
            var input = new Windows.Win32.UI.Input.KeyboardAndMouse.INPUT();
            input.Anonymous.ki = new Windows.Win32.UI.Input.KeyboardAndMouse.KEYBDINPUT()
            {
                wVk = accessoryVirtualKeyToPress,
                dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
//                dwExtraInfo = currentThreadMessageExtraInfo,
            };
            input.type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD;
            inputs.Add(input);
        }

        // step 8: send the inputs
        // NOTE: these will be effectively OR'd against the current keyboard state; there is a non-zero chance that a key might be pressed between steps 2 and step 8 and that this input will be corrupted (NOTE: there is no obvious way to avoid this scenario in this function or detect its occurrence)
        var inputsAsArray = inputs.ToArray();
        uint sendInputResult;
        unsafe 
        {
            fixed (Windows.Win32.UI.Input.KeyboardAndMouse.INPUT* pointerToInputsAsArray = inputsAsArray)
            {
                sendInputResult = Windows.Win32.PInvoke.SendInput((uint)inputsAsArray.Length, pointerToInputsAsArray, sizeof(Windows.Win32.UI.Input.KeyboardAndMouse.INPUT));
            }
        }
        if (sendInputResult != inputsAsArray.Length)
        {
            // if all of the inputs were not send, we experienced an error
            var win32Error = (uint)System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<ISynthesizeKeyInputError>(new ISynthesizeKeyInputError.Win32Error(win32Error));
        }

        return MorphicResult.OkResult();
    }

    private static Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY? ConvertAccessoryKeyToWin32Vkey(AccessoryKey accessoryKey)
    {
        return accessoryKey switch
        {
            //AccessoryKey.Alt => Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_MENU,
            AccessoryKey.LeftAlt => Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_LMENU,
            AccessoryKey.RightAlt => Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_RMENU,
            //
            AccessoryKey.CapsLock => Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_CAPITAL,
            //
            //AccessoryKey.Control => Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_CONTROL,
            AccessoryKey.LeftControl => Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_LCONTROL,
            AccessoryKey.RightControl => Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_RCONTROL,
            //
            //AccessoryKey.Shift => Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_SHIFT,
            AccessoryKey.LeftShift => Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_LSHIFT,
            AccessoryKey.RightShift => Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_RSHIFT,
            //
            AccessoryKey.LeftWindows => Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_LWIN,
            AccessoryKey.RightWindows => Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_RWIN,
            //
            _ => throw new Exception("invalid code path"),
        };
    }

    private static Dictionary<Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY, short> GetAllAccessoryVirtualKeysStates()
    {
        Dictionary<Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY, short> result = [];

        foreach (var vKey in s_allAccessoryVirtualKeys)
        {
            var state = Windows.Win32.PInvoke.GetAsyncKeyState((int)vKey);
            result[vKey] = state;
        }

        return result;
    }
}
