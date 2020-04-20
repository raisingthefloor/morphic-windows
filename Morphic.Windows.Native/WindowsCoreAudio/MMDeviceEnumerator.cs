//
// MMDeviceEnumerator.cs
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
                if (mmDeviceEnumeratorAsNullable == null)
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

            if (immDevice == null)
            {
                // NOTE: this code should never be executed since GetDefaultAudioEndpoint should have returned an HRESULT of E_POINTER if it failed
                throw new COMException("IMMDeviceEnumerator.GetDefaultAudioEndpoint returned a null pointer", new NullReferenceException());
            }

            var mmDevice = MMDevice.CreateFromIMMDevice(immDevice!);
            return mmDevice;
        }
    }
}
