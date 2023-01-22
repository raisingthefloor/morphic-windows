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

namespace Morphic.WindowsNative.WindowsCoreAudio;

using Morphic.Core;
using Morphic.WindowsNative.WindowsCom;
using Morphic.WindowsNative.WindowsCoreAudio.Exceptions;
using System;
using System.Runtime.InteropServices;

internal class MMDeviceEnumerator
{
    private const String CLSID_MMDeviceEnumerator = "BCDE0395-E52F-467C-8E3D-C4579291692E";

    private IMMDeviceEnumerator _mmDeviceEnumerator;

    // NOTE: this constructor throws COMException if the underlying COM object cannot be initialized
    private MMDeviceEnumerator(IMMDeviceEnumerator mmDeviceEnumerator)
    {
        _mmDeviceEnumerator = mmDeviceEnumerator;
    }

    public static MorphicResult<MMDeviceEnumerator, WindowsComError> CreateNew()
    {
        // get a Type reference for MMDeviceEnumerator
        Type MMDeviceEnumeratorType;
        try
        {
            MMDeviceEnumeratorType = Type.GetTypeFromCLSID(new Guid(CLSID_MMDeviceEnumerator), true)!;
        }
        catch
        {
            // TODO: consider providing a more specific exception result
            return MorphicResult.ErrorResult(WindowsComError.ComException(new COMException()));
        }

        MMDeviceEnumerator result;
        try
        {
            // NOTE: objects created by Activator.CreateInstance do not need to be manually freed
            var mmDeviceEnumeratorAsNullable = Activator.CreateInstance(MMDeviceEnumeratorType) as IMMDeviceEnumerator;
            if (mmDeviceEnumeratorAsNullable is null)
            {
                throw new COMException();
            }
            result = new MMDeviceEnumerator(mmDeviceEnumeratorAsNullable!);
        }
        catch
        {
            // TODO: in the future, consider throwing different exceptions for different failure conditions
            throw new COMException();
        }

        return MorphicResult.OkResult(result);
    }

    public MorphicResult<MMDevice, WindowsComError> GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role)
    {
        IMMDevice? immDevice;
        var result = _mmDeviceEnumerator.GetDefaultAudioEndpoint(dataFlow, role, out immDevice);
        if (result != ExtendedPInvoke.S_OK)
        {
            if (result == ExtendedPInvoke.E_NOTFOUND)
            {
                throw new NoDeviceIsAvailableException();
            }
            else
            {
                // TODO: consider throwing more granular exceptions here
                var comException = new COMException("IMMDeviceEnumerator.GetDefaultAudioEndpoint failed", Marshal.GetExceptionForHR(result));
                return MorphicResult.ErrorResult(WindowsComError.ComException(comException));
            }
        }

        if (immDevice is null)
        {
            // NOTE: this code should never be executed since GetDefaultAudioEndpoint should have returned an HRESULT of E_POINTER if it failed
            var comException = new COMException("IMMDeviceEnumerator.GetDefaultAudioEndpoint returned a null pointer", new NullReferenceException());
            return MorphicResult.ErrorResult(WindowsComError.ComException(comException));
        }

        var mmDevice = MMDevice.CreateFromIMMDevice(immDevice!);
        return MorphicResult.OkResult(mmDevice);
    }
}
