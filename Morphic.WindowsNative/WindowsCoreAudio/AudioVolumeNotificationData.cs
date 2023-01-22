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

using System;
using System.Runtime.InteropServices;

internal struct AudioVolumeNotificationData
{
    public Guid GuidEventContext;
    public bool Muted;
    public float MasterVolume;
    public float[] ChannelVolumes;

    [StructLayout(LayoutKind.Sequential)]
    private struct AUDIO_VOLUME_NOTIFICATION_DATA
    {
        public Guid guidEventContext;
        public bool bMuted;
        public float fMasterVolume;
        public uint nChannels;
        // NOTE: afChannelVolumes is an array, but C# doesn't seem to want to marshal data to a variable-length array (i.e. System.AccessViolatedException, etc.) so we do manual marshalling math instead
        public float afChannelVolumes;
    }

    public static AudioVolumeNotificationData MarshalFromIntPtr(IntPtr ptr)
    {
        // capture the structure (including the count of how many channels are represented...but not the full array of afChannelVolumes data)
        var audioVolumeNotificationData = Marshal.PtrToStructure<AUDIO_VOLUME_NOTIFICATION_DATA>(ptr);
        var numberOfChannels = audioVolumeNotificationData.nChannels;

        // determine the location of audioVolumeNotificationData.afChannelVolumes in memory so that we can capture all channels' volumes
        var offsetOfChannelVolumes = Marshal.OffsetOf<AUDIO_VOLUME_NOTIFICATION_DATA>("afChannelVolumes");
        var pointerToChannelVolumes = IntPtr.Add(ptr, (int)offsetOfChannelVolumes);

        // capture the channel volumes [through manual marshalling]
        var channelVolumes = new float[numberOfChannels];
        var pointerToCurrentChannelVolume = pointerToChannelVolumes;
        for (var index = 0; index < numberOfChannels; index += 1)
        {
            channelVolumes[index] = Marshal.PtrToStructure<float>(pointerToCurrentChannelVolume);
            pointerToCurrentChannelVolume = IntPtr.Add(pointerToCurrentChannelVolume, Marshal.SizeOf<float>());
        }

        var result = new AudioVolumeNotificationData()
        {
            GuidEventContext = audioVolumeNotificationData.guidEventContext,
            Muted = audioVolumeNotificationData.bMuted,
            MasterVolume = audioVolumeNotificationData.fMasterVolume,
            ChannelVolumes = channelVolumes
        };

        return result;
    }
}