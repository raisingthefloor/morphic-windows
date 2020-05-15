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

namespace Morphic.Settings.Spi
{
    /// <summary>
    /// A finalizer that calls into System Parameters Info (SPI)
    /// </summary>
    class SystemParametersInfoSettingsFinalizer : SettingFinalizer
    {

        /// <summary>
        /// The handler description
        /// </summary>
        public SystemParametersInfoSettingFinalizerDescription Description;

        /// <summary>
        /// The SPI object to use
        /// </summary>
        public ISystemParametersInfo systemParametersInfo;

        /// <summary>
        /// Create a new SPI handler
        /// </summary>
        /// <param name="description"></param>
        /// <param name="systemParametersInfo"></param>
        /// <param name="logger"></param>
        public SystemParametersInfoSettingsFinalizer(SystemParametersInfoSettingFinalizerDescription description, ISystemParametersInfo systemParametersInfo, ILogger<SystemParametersInfoSettingsFinalizer> logger)
        {
            Description = description;
            this.logger = logger;
            this.systemParametersInfo = systemParametersInfo;
        }

        /// <summary>
        /// The logger to use
        /// </summary>
        private readonly ILogger<SystemParametersInfoSettingsFinalizer> logger;

        public override Task<bool> Run()
        {
            var success = false;
            try
            {
                logger.LogInformation("SPI({0})", Description.Action);
                success = systemParametersInfo.Call(Description.Action, Description.Parameter1, Description.Parameter2, Description.UpdateUserProfile, Description.SendChange);
            }catch (Exception e)
            {
                logger.LogError(e, "Failed to set sysetem parameters info");
            }
            return Task.FromResult(success);
        }

    }

}
