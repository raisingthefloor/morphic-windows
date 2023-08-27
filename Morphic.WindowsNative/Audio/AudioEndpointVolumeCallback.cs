// Copyright 2020-2023 Raising the Floor - US, Inc.
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.Audio;

internal class AudioEndpointVolumeCallback : WindowsCoreAudio.IAudioEndpointVolumeCallback
{
    public delegate void CallbackReceived(WindowsCoreAudio.AudioVolumeNotificationData audioVolumeNotificationData);
    private CallbackReceived _callbackReceivedHandler;

    public AudioEndpointVolumeCallback(CallbackReceived callbackReceivedHandler)
    {
        _callbackReceivedHandler = callbackReceivedHandler;
    }

    // see: https://github.com/MicrosoftDocs/win32/blob/docs/desktop-src/CoreAudio/endpoint-volume-controls.md
    public void OnNotify(IntPtr pNotify /* PAUDIO_VOLUME_NOTIFICATION_DATA */)
    {
        // NOTE: as the incoming data is variable in length, we need to receive it as an IntPtr and then call a helper function to marshal it properly
        var audioVolumeNotificationData = WindowsCoreAudio.AudioVolumeNotificationData.MarshalFromIntPtr(pNotify);

        // NOTE: this function must NOT be blocking; as such we call the managed event asynchronously and return immediately...avoiding any potential of waiting on synchronization locks, etc.
        // see: https://github.com/MicrosoftDocs/sdk-api/blob/docs/sdk-api-src/content/endpointvolume/nn-endpointvolume-iaudioendpointvolumecallback.md
        Task.Run(() =>
        {
            _callbackReceivedHandler(audioVolumeNotificationData);
        });
    }
}
