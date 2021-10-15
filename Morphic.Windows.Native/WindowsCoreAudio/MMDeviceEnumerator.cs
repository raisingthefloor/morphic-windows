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
    using Exceptions;

    internal class MMDeviceEnumerator
    {
        private readonly String CLSID_MMDeviceEnumerator = "BCDE0395-E52F-467C-8E3D-C4579291692E";

        private IMMDeviceEnumerator _mmDeviceEnumerator;

        // NOTE: this constructor throws COMException if the underlying COM object cannot be initialized
        public MMDeviceEnumerator()
        {
            // get a Type reference for MMDeviceEnumerator
            Type MMDeviceEnumeratorType;
            try
            {
                MMDeviceEnumeratorType = Type.GetTypeFromCLSID(new Guid(CLSID_MMDeviceEnumerator), true);
            }
            catch
            {
                throw new COMException();
            }

            try
            {
                // NOTE: objects created by Activator.CreateInstance do not need to be manually freed
                var mmDeviceEnumeratorAsNullable = Activator.CreateInstance(MMDeviceEnumeratorType) as IMMDeviceEnumerator;
                if (mmDeviceEnumeratorAsNullable is null)
                {
                    throw new COMException();
                }
                _mmDeviceEnumerator = mmDeviceEnumeratorAsNullable!;
            }
            catch
            {
                // TODO: in the future, consider throwing different exceptions for different failure conditions
                throw new COMException();
            }
        }

        public MMDevice GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role)
        {
            IMMDevice? immDevice;
            var result = _mmDeviceEnumerator.GetDefaultAudioEndpoint(dataFlow, role, out immDevice);
            if (result != WindowsApi.S_OK)
            {
                if (result == WindowsApi.E_NOTFOUND)
                {
                    throw new NoDeviceIsAvailableException();
                }
                else
                {
                    // TODO: consider throwing more granular exceptions here
                    throw new COMException("IMMDeviceEnumerator.GetDefaultAudioEndpoint failed", Marshal.GetExceptionForHR(result));
                }
            }

            if (immDevice is null)
            {
                // NOTE: this code should never be executed since GetDefaultAudioEndpoint should have returned an HRESULT of E_POINTER if it failed
                throw new COMException("IMMDeviceEnumerator.GetDefaultAudioEndpoint returned a null pointer", new NullReferenceException());
            }

            var mmDevice = MMDevice.CreateFromIMMDevice(immDevice!);
            return mmDevice;
        }
    }
}
