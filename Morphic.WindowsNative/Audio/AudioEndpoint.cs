// Copyright 2020-2022 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/master/LICENSE.txt
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

namespace Morphic.WindowsNative.Audio
{
    using Morphic.WindowsNative.WindowsCom;
    using Morphic.WindowsNative.WindowsCoreAudio;
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    public class AudioEndpoint: IDisposable
    {
        private IAudioEndpointVolume _audioEndpointVolume;
        private AudioEndpointVolumeCallback? _audioEndpointVolumeCallback;
        private bool disposedValue;

        private float? _lastMasterVolumeLevel;
        private bool? _lastMasterMuteState;

        private AudioEndpoint(IAudioEndpointVolume audioEndpointVolume)
        {
            this._audioEndpointVolume = audioEndpointVolume;
        }

        protected virtual void Dispose(bool disposing)
        {
            // OBSERVATION: in our early testing, Dispose was never called with _audioEndpointVolumeCallback _not_ set to true, even when the application was shutting down;
            //              we should do some testing and see if it's possible to make sure that this "unregister" always gets called...just in case Windows doesn't like having
            //              registrations sticking around for applications which aren't still running

            // before disposing of any managed objects, unregister our callback
            try
            {
                if (_audioEndpointVolumeCallback is not null)
                {
                    _audioEndpointVolume.UnregisterControlChangeNotify(_audioEndpointVolumeCallback!);
                }
            }
            catch
            {
                // ignore any exception
                Debug.Assert(false, "Could not unregister unmanaged notification callback");
            }

            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // free unmanaged resources

                disposedValue = true;
            }
        }

        ~AudioEndpoint()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
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

        private void UpdateUnmanagedNotificationRegistration()
        {
            var shouldBeRegisteredForNotifications = false;
            // if any events are registered by our caller, we need to be registered for unmanaged volume/mute state change notifications
            //
            // volume level
            if (_masterVolumeLevelChangedEvent is not null)
            {
                shouldBeRegisteredForNotifications = true;
            }
            //
            // mute state
            if (_masterMuteStateChangedEvent is not null)
            {
                shouldBeRegisteredForNotifications = true;
            }

            if (shouldBeRegisteredForNotifications == true)
            {
                // if we have subscribed events and are _not_ already registered for notifications, register for notifications now
                if (_audioEndpointVolumeCallback is null)
                {
                    _audioEndpointVolumeCallback = new AudioEndpointVolumeCallback(this.AudioVolumeNotificationCallback);
                    _audioEndpointVolume.RegisterControlChangeNotify(_audioEndpointVolumeCallback);
                }
            }
            else
            {
                // unregister for notifications if we are already registered...but no longer have any subscribed events
                if (_audioEndpointVolumeCallback is not null)
                {
                    _audioEndpointVolume.UnregisterControlChangeNotify(_audioEndpointVolumeCallback!);
                }
            }
        }

        private void AudioVolumeNotificationCallback(AudioVolumeNotificationData audioVolumeNotificationData)
        {
            if (_lastMasterVolumeLevel != audioVolumeNotificationData.MasterVolume)
            {
                _lastMasterVolumeLevel = audioVolumeNotificationData.MasterVolume;
                _masterVolumeLevelChangedEvent?.Invoke(this, new MasterVolumeLevelChangedEventArgs() { VolumeLevel = audioVolumeNotificationData.MasterVolume });
            }

            if (_lastMasterMuteState != audioVolumeNotificationData.Muted)
            {
                _lastMasterMuteState = audioVolumeNotificationData.Muted;
                _masterMuteStateChangedEvent?.Invoke(this, new MasterMuteStateChangedEventArgs() { MuteState = audioVolumeNotificationData.Muted });
            }
        }

        public class MasterVolumeLevelChangedEventArgs : EventArgs
        {
            public float VolumeLevel;
        }
        private EventHandler<MasterVolumeLevelChangedEventArgs> _masterVolumeLevelChangedEvent;
        public event EventHandler<MasterVolumeLevelChangedEventArgs> MasterVolumeLevelChangedEvent
        {
            add
            {
                // if we have not already captured the master volume level, do so now
                if (_lastMasterVolumeLevel is null)
                {
                    try
                    {
                        this.GetMasterVolumeLevel();
                    }
                    catch { }
                }

                _masterVolumeLevelChangedEvent += value;
                this.UpdateUnmanagedNotificationRegistration();
            }
            remove
            {
                if (_masterVolumeLevelChangedEvent is not null)
                {
                    _masterVolumeLevelChangedEvent -= value;
                }
                this.UpdateUnmanagedNotificationRegistration();
            }
        }

        public Single GetMasterVolumeLevel()
        {
            // get the master volume level
            Single volumeLevelScalar;
            var result = this._audioEndpointVolume.GetMasterVolumeLevelScalar(out volumeLevelScalar);
            if (result != WindowsApi.S_OK)
            {
                // TODO: consider throwing more granular exceptions here
                throw new COMException("IAudioEndpointVolume.GetMasterVolumeLevelScalar failed", Marshal.GetExceptionForHR(result));
            }

            _lastMasterVolumeLevel = volumeLevelScalar;

            return volumeLevelScalar;
        }

        public void SetMasterVolumeLevel(Single volumeLevel)
        {
            if (volumeLevel < 0.0 || volumeLevel > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(volumeLevel));
            }

            // set the master volume level
            var result = this._audioEndpointVolume.SetMasterVolumeLevelScalar(volumeLevel, IntPtr.Zero);
            if (result != WindowsApi.S_OK)
            {
                // TODO: consider throwing more granular exceptions here
                throw new COMException("IAudioEndpointVolume.GetMasterVolumeLevelScalar failed", Marshal.GetExceptionForHR(result));
            }
        }

        public class MasterMuteStateChangedEventArgs : EventArgs
        {
            public bool MuteState;
        }
        private EventHandler<MasterMuteStateChangedEventArgs> _masterMuteStateChangedEvent;
        public event EventHandler<MasterMuteStateChangedEventArgs> MasterMuteStateChangedEvent
        {
            add
            {
                // if we have not already captured the master mute state, do so now
                if (_lastMasterMuteState is null)
                {
                    try
                    {
                        this.GetMasterMuteState();
                    }
                    catch { }
                }

                _masterMuteStateChangedEvent += value;
                this.UpdateUnmanagedNotificationRegistration();
            }
            remove
            {
                _masterMuteStateChangedEvent -= value;
                this.UpdateUnmanagedNotificationRegistration();
            }
        }

        public Boolean GetMasterMuteState()
        {
            // get the master mute state
            Int32 isMutedAsInt32;
            var result = this._audioEndpointVolume.GetMute(out isMutedAsInt32);
            if (result != WindowsApi.S_OK)
            {
                // TODO: consider throwing more granular exceptions here
                throw new COMException("IAudioEndpointVolume.GetMute failed", Marshal.GetExceptionForHR(result));
            }

            var muteState = (isMutedAsInt32 != 0) ? true : false;

            _lastMasterMuteState = muteState;

            return muteState;
        }

        public void SetMasterMuteState(Boolean muteState)
        {
            // set the master mute state
            var result = this._audioEndpointVolume.SetMute(muteState ? 1 : 0, IntPtr.Zero);
            if (result != WindowsApi.S_OK)
            {
                // TODO: consider throwing more granular exceptions here
                throw new COMException("IAudioEndpointVolume.SetMute failed", Marshal.GetExceptionForHR(result));
            }
        }
    }
}
