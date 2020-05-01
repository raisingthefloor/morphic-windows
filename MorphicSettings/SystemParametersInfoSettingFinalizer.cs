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
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace MorphicSettings
{
    class SystemParametersInfoSettingsFinalizer : SettingFinalizer
    {

        public SystemParametersInfoSettingFinalizerDescription Description;

        public SystemParametersInfoSettingsFinalizer(SystemParametersInfoSettingFinalizerDescription description, ILogger<SystemParametersInfoSettingsFinalizer> logger)
        {
            Description = description;
            this.logger = logger;
        }

        private readonly ILogger<SystemParametersInfoSettingsFinalizer> logger;

        public override Task<bool> Run()
        {
            var param2Handle = GCHandle.Alloc(Description.Parameter2);
            int param3 = 0;
            if (Description.UpdateUserProfile)
            {
                param3 |= 0x1;
            }
            if (Description.SendChange)
            {
                param3 |= 0x2;
            }
            var result = SystemParametersInfo.SystemParametersInfoW((int)Description.Action, Description.Parameter1, GCHandle.ToIntPtr(param2Handle), param3);
            param2Handle.Free();
            return Task.FromResult(result);
        }

    }

}
