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

[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    // RegisterControlChangeNotify
    public Int32 RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);

    // UnregisterControlChangeNotify
    public Int32 UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);

    // GetChannelCount
    public Int32 GetChannelCount(out uint pnChannelCount);

    // SetMasterVolumeLevel
    public Int32 SetMasterVolumeLevel(float fLevelDB, IntPtr /* (IntPtr.Zero) */ pguidEventContext);

    // SetMasterVolumeLevelScalar
    public Int32 SetMasterVolumeLevelScalar(float fLevel, IntPtr /* (IntPtr.Zero) */ pguidEventContext);

    // GetMasterVolumeLevel
    public Int32 GetMasterVolumeLevel(out float pfLevelDB);

    // GetMasterVolumeLevelScalar
    public Int32 GetMasterVolumeLevelScalar(out float pfLevel);

    // SetChannelVolumeLevel
    public Int32 SetChannelVolumeLevel(uint nChannel, float fLevelDB, IntPtr /* (IntPtr.Zero) */ pguidEventContext);

    // SetChannelVolumeLevelScalar
    public Int32 SetChannelVolumeLevelScalar(uint nChannel, float fLevel, IntPtr /* (IntPtr.Zero) */ pguidEventContext);

    // GetChannelVolumeLevel
    public Int32 GetChannelVolumeLevel(uint nChannel, out float fLevelDB);

    // GetChannelVolumeLevelScalar
    public Int32 GetChannelVolumeLevelScalar(uint nChannel, out float fLevel);

    // SetMute
    public Int32 SetMute(Int32 bMute, IntPtr /* (IntPtr.Zero) */ pguidEventContext);

    // GetMute
    public Int32 GetMute(out Int32 bMute);

    // GetVolumeStepInfo
    public Int32 GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);

    // VolumeStepUp
    public Int32 VolumeStepUp(IntPtr /* (IntPtr.Zero) */ pguidEventContext);

    // VolumeStepDown
    public Int32 VolumeStepDown(IntPtr /* (IntPtr.Zero) */ pguidEventContext);

    // QueryHardwareSupport
    public Int32 QueryHardwareSupport(out uint pdwHardwareSupportMask);

    // GetVolumeRange
    public Int32 GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
}
