//
// MMDevice.cs
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
    internal class MMDevice
    {
        private IMMDevice _immDevice;

        private MMDevice(IMMDevice immDevice)
        {
            _immDevice = immDevice;
        }

        internal static MMDevice CreateFromIMMDevice(IMMDevice immDevice) {
            var result = new MMDevice(immDevice);
            //
            return result;
        }

        public Object Activate(Guid iid, CLSCTX clsCtx)
        {
            Object? @interface;
            var result = _immDevice.Activate(iid, clsCtx, IntPtr.Zero, out @interface);
            if (result != WindowsApi.S_OK)
            {
                // TODO: consider throwing more granular exceptions here
                throw new COMException("IMMDeviceEnumerator.GetDefaultAudioEndpoint failed", Marshal.GetExceptionForHR(result));
            }

            if (@interface == null)
            {
                // NOTE: this code should never be executed since Activate should have returned an HRESULT of E_POINTER if it failed
                throw new COMException("IMMDevice.Activate returned a null pointer", new NullReferenceException());
            }

            return @interface!;
        }

    }
}
