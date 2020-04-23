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

using Morphic.Windows.Native.WindowsCoreAudio;
using System;
using System.Runtime.InteropServices;

namespace Morphic.Windows.Native
{
    public class AudioEndpoint
    {
        private IAudioEndpointVolume _audioEndpointVolume;

        private AudioEndpoint(IAudioEndpointVolume audioEndpointVolume)
        {
            _audioEndpointVolume = audioEndpointVolume;
        }

        public static AudioEndpoint GetDefaultAudioOutputEndpoint()
        {
            // get a reference to our default output device
            var mmDeviceEnumerator = new MMDeviceEnumerator();
            //
            // NOTE: .NET should automatically release the defaultAudioOutputEndpoint
            var defaultAudioOutputEndpoint = mmDeviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);

            // activate the endpoint so we can read/write its volume and mute state, etc.
            IAudioEndpointVolume audioEndpointVolume;
            try
            {
                Guid IID_IAudioEndpointVolume = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
                var audioEndpointVolumeAsObject = defaultAudioOutputEndpoint.Activate(IID_IAudioEndpointVolume, CLSCTX.CLSCTX_INPROC_SERVER);
                audioEndpointVolume = (IAudioEndpointVolume)audioEndpointVolumeAsObject;
            }
            catch
            {
                // if we could not activate the endpoint, re-throw an exception
                throw;
            }

            return new AudioEndpoint(audioEndpointVolume);
        }

        public Single GetMasterVolumeLevel()
        {
            // get the master volume level
            Single volumeLevelScalar;
            var result = _audioEndpointVolume.GetMasterVolumeLevelScalar(out volumeLevelScalar);
            if (result != WindowsApi.S_OK)
            {
                // TODO: consider throwing more granular exceptions here
                throw new COMException("IAudioEndpointVolume.GetMasterVolumeLevelScalar failed", Marshal.GetExceptionForHR(result));
            }

            return volumeLevelScalar;
        }

        public void SetMasterVolumeLevel(Single volumeLevel)
        {
            if (volumeLevel < 0.0 || volumeLevel > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(volumeLevel));
            }

            // set the master volume level
            var result = _audioEndpointVolume.SetMasterVolumeLevelScalar(volumeLevel, IntPtr.Zero);
            if (result != WindowsApi.S_OK)
            {
                // TODO: consider throwing more granular exceptions here
                throw new COMException("IAudioEndpointVolume.GetMasterVolumeLevelScalar failed", Marshal.GetExceptionForHR(result));
            }
        }

        public Boolean GetMasterMuteState()
        {
            // get the master mute state
            Int32 isMutedAsInt32;
            var result = _audioEndpointVolume.GetMute(out isMutedAsInt32);
            if (result != WindowsApi.S_OK)
            {
                // TODO: consider throwing more granular exceptions here
                throw new COMException("IAudioEndpointVolume.GetMute failed", Marshal.GetExceptionForHR(result));
            }

            return (isMutedAsInt32 != 0) ? true : false;
        }

        public void SetMasterMuteState(Boolean muteState)
        {
            // set the master mute state
            var result = _audioEndpointVolume.SetMute(muteState ? 1 : 0, IntPtr.Zero);
            if (result != WindowsApi.S_OK)
            {
                // TODO: consider throwing more granular exceptions here
                throw new COMException("IAudioEndpointVolume.SetMute failed", Marshal.GetExceptionForHR(result));
            }
        }
    }
}
