//
// Processor.cs
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

namespace Morphic.Windows.Native
{
    public class Processor
    {
        public enum ProcessorArchitecture
        {
            Arm32,
            Arm64,
            X86,
            X64
        }

        public static ProcessorArchitecture? GetProcessorArchitecture()
        {
            // var cSystemInfo = new WindowsApi.SYSTEM_INFO();
            WindowsApi.SYSTEM_INFO cSystemInfo;
            WindowsApi.GetSystemInfo(out cSystemInfo);

            switch (cSystemInfo.dummyUnion.DUMMYSTRUCTNAME.wProcessorArchitecture)
            {
                case (UInt16)WindowsApi.ProcessorArchitecture.IA32:
                    return ProcessorArchitecture.X86;
                case (UInt16)WindowsApi.ProcessorArchitecture.ARM:
                    return ProcessorArchitecture.Arm32;
                //case 6: // PROCESSOR_ARCHITECTURE_IA64
                //    return ProcessorArchitecture.Itanium;
                case (UInt16)WindowsApi.ProcessorArchitecture.AMD64:
                    return ProcessorArchitecture.X64;
                //case 11:
                //    return ProcessorArchitecture.Neutral;
                case (UInt16)WindowsApi.ProcessorArchitecture.ARM64:
                    return ProcessorArchitecture.Arm64;
                //case 14:
                //    // NOTE: in our tests, x86 emulation on ARM64 just returned "PROCESSOR_ARCHITECTURE_INTEL" (0)
                //    return ProcessorArchitecture.X86;
                case (UInt16)WindowsApi.ProcessorArchitecture.UNKNOWN:
                default:
                    return null;
            }
        }
    }
}
