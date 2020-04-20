//
// IMMDevice.cs
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
    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        // Activate
        public Int32 Activate(Guid iid, CLSCTX dwClsCtx, IntPtr /* (IntPtr.Zero) */ activationParams, [MarshalAs(UnmanagedType.IUnknown)] out Object? @interface);

        // OpenPropertyStore

        // GetId

        // GetState

    }
}
