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
    //[ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        // RegisterControlChangeNotify
        public Int32 RegisterControlChangeNotify(IntPtr pNotify);

        // UnregisterControlChangeNotify
        public Int32 UnregisterControlChangeNotify(IntPtr pNotify);

        // GetChannelCount
        public Int32 GetChannelCount(out UInt32 pnChannelCount);

        // SetMasterVolumeLevel
        public Int32 SetMasterVolumeLevel(Single fLevelDB, IntPtr /* (IntPtr.Zero) */ pguidEventContext);

        // SetMasterVolumeLevelScalar
        public Int32 SetMasterVolumeLevelScalar(Single fLevel, IntPtr /* (IntPtr.Zero) */ pguidEventContext);

        // GetMasterVolumeLevel
        public Int32 GetMasterVolumeLevel(out Single pfLevelDB);

        // GetMasterVolumeLevelScalar
        public Int32 GetMasterVolumeLevelScalar(out Single pfLevel);

        // SetChannelVolumeLevel
        public Int32 SetChannelVolumeLevel(UInt32 nChannel, Single fLevelDB, IntPtr /* (IntPtr.Zero) */ pguidEventContext);

        // SetChannelVolumeLevelScalar
        public Int32 SetChannelVolumeLevelScalar(UInt32 nChannel, Single fLevel, IntPtr /* (IntPtr.Zero) */ pguidEventContext);

        // GetChannelVolumeLevel
        public Int32 GetChannelVolumeLevel(UInt32 nChannel, out Single fLevelDB);

        // GetChannelVolumeLevelScalar
        public Int32 GetChannelVolumeLevelScalar(UInt32 nChannel, out Single fLevel);

        // SetMute
        public Int32 SetMute(Int32 bMute, IntPtr /* (IntPtr.Zero) */ pguidEventContext);

        // GetMute
        public Int32 GetMute(out Int32 bMute);

        // GetVolumeStepInfo
        public Int32 GetVolumeStepInfo(out UInt32 pnStep, out UInt32 pnStepCount);

        // VolumeStepUp
        public Int32 VolumeStepUp(IntPtr /* (IntPtr.Zero) */ pguidEventContext);

        // VolumeStepDown
        public Int32 VolumeStepDown(IntPtr /* (IntPtr.Zero) */ pguidEventContext);

        // QueryHardwareSupport
        public Int32 QueryHardwareSupport(out UInt32 pdwHardwareSupportMask);

        // GetVolumeRange
        public Int32 GetVolumeRange(out Single pflVolumeMindB, out Single pflVolumeMaxdB, out Single pflVolumeIncrementdB);
    }
}
