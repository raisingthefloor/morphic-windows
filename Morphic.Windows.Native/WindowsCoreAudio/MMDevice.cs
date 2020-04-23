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
