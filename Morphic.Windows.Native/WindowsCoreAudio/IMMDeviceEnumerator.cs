//
// IMMDeviceEnumerator.cs
// Morphic support library for Windows
//
// Copyright © 2020 Raising the Floor -- US Inc. All rights reserved.
//
// The R&D leading to these results received funding from the
// Department of Education - Grant H421A150005 (GPII-APCP). However,
// these results do not necessarily represent the policy of the
// Department of Education, and you should not assume endorsement by the
// Federal Government.

using System;
using System.Runtime.InteropServices;

namespace Morphic.Windows.Native.WindowsCoreAudio
{
    //[ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        // EnumAudioEndpoints
        // NOTE: EnumAudioEndpoints is a filler declaration (required for COM); change IntPtr to IMMDeviceCollection? if we implement this function
        public Int32 EnumAudioEndpoints(EDataFlow dataFlow, UInt32 stateMask, out IntPtr devices);

        // GetDefaultAudioEndpoint
        public Int32 GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, [MarshalAs(UnmanagedType.Interface)] out IMMDevice? endpoint);

        // GetDevice

        // RegisterEndpointNotificationCallback

        // UnregisterEndpointNotificationCallback

    }
}
