//
// AudioEndpoint.cs
// Morphic support library for Windows
//
// Copyright © 2020 Raising the Floor -- US Inc. All rights reserved.
//
// The R&D leading to these results received funding from the
// Department of Education - Grant H421A150005 (GPII-APCP). However,
// these results do not necessarily represent the policy of the
// Department of Education, and you should not assume endorsement by the
// Federal Government.

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
