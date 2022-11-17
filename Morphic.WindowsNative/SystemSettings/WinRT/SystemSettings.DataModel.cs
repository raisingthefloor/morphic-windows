// Copyright 2018-2022 Raising the Floor - US, Inc.
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

namespace SystemSettings.DataModel
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Interface for the settings classes, instantiated by the GetSettings exports.
    /// </summary>
    /// <remarks>
    /// Most of the information was taken from the debug symbols (PDB) for the relevent DLLs. The symbols
    /// don't describe the interface, just the classes that implement it (the "vtable"). This contains the
    /// method names (and order), and vague information on the parameters (no names, and, er, de-macro'd types).
    ///
    /// Visual Studio was used to obtain the names by first creating a method with any name, then stepping into the
    /// native code from the call with the debugger where the function name will be displayed in the disassembled code.
    ///
    /// The binding of methods isn't by name, but by order, which is why the "unknown" methods must remain.
    /// Not all methods work for some type(s) of settings.
    /// </remarks>
    [ComImport, Guid("40C037CC-D8BF-489E-8697-D66BAA3221BF"), InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    public interface ISettingItem
    {
        int Id { get; }
        SettingType Type { get; }
        bool IsSetByGroupPolicy { get; }
        bool IsEnabled { get; }
        bool IsApplicable { get; }

        // Not always available, sometimes looks like a resource ID
        string Description
        {
            [return: MarshalAs(UnmanagedType.HString)]
            get;
        }

        // Unknown
        bool IsUpdating { get; }

        // For Type = Boolean, List, Range, String
        [return: MarshalAs(UnmanagedType.IInspectable)]
        object GetValue(
            // Normally "Value"
            [MarshalAs(UnmanagedType.HString)] string name);

        int SetValue(
            // Normally "Value"
            [MarshalAs(UnmanagedType.HString)] string name,
            [MarshalAs(UnmanagedType.IInspectable)] object? pValue); // NOTE: the pValue parameter is not nullable in the CsWinRT-derived interface (but that may be an artifact of CsWinRT and WinRT)

        // Unknown usage
        int GetProperty(string name);
        int SetProperty(string name, object pValue);

        // For Type = Action - performs the action.
        // NOTE: in WinRT, the window type is global::Windows.UI.Core.CoreWindow
        IntPtr Invoke(IntPtr window, Rect rect);

        // SettingChanged event
        // NOTE: in WinRT, the event type is global::Windows.Foundation.TypedEventHandler<object, string>
        event EventHandler<string> SettingChanged;

        // NOTE: none of the following functions appear to be part of the ISettingItem interface; they might be part of a superinterface or they might just be incorrect; we do not use them

        // Unknown - setter for IsUpdating
        bool IsUpdating2 { set; }

        // Unknown
        int GetInitializationResult();
        int DoGenericAsyncWork();
        int StartGenericAsyncWork();
        int SetSkipConcurrentOperations(bool flag);

        // These appear to be base implementations overridden by the above.
        bool GetValue2 { get; }
        IntPtr unknown_SetValue1();
        IntPtr unknown_SetValue2();
        IntPtr unknown_SetValue3();

        // Unknown usage
        IntPtr GetNamedValue(
            [MarshalAs(UnmanagedType.HString)] string name
            //[MarshalAs(UnmanagedType.IInspectable)] object unknown
            );

        IntPtr SetNullValue();

        // For Type=List:
        IntPtr GetPossibleValues(out IList<object> value);

        // There are more unknown methods.
    }

    /// <summary>The type of setting.</summary>
    public enum SettingType : int
    {
        Plugin = unchecked((int)0xffffffff),
        Custom = unchecked((int)0),
        DisplayString = unchecked((int)0x1),
        LabeledString = unchecked((int)0x2),
        Boolean = unchecked((int)0x3),
        Range = unchecked((int)0x4),
        String = unchecked((int)0x5),
        List = unchecked((int)0x6),
        Action = unchecked((int)0x7),
        SettingCollection = unchecked((int)0x8),
    }

    /// <summary>
    /// Used by ISettingsItem.Invoke (reason unknown).
    /// </summary>
    public struct Rect
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;
    }

}
