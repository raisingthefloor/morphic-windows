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

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Settings.Process
{
    public class ProcessSettingFinalizer: SettingFinalizer
    {

        public ProcessSettingFinalizer(ProcessSettingFinalizerDescription description, IProcessManager processManager, ILogger<ProcessSettingFinalizer> logger)
        {
            Description = description;
            this.processManager = processManager;
            this.logger = logger;
        }

        public ProcessSettingFinalizerDescription Description { get; }

        private readonly IProcessManager processManager;

        private readonly ILogger<ProcessSettingFinalizer> logger;

        public override async Task<bool> Run()
        {
            try
            {
                switch (Description.Action)
                {
                    case ProcessAction.Start:
                        return await Start();
                    case ProcessAction.Stop:
                        return await Stop();
                    case ProcessAction.Restart:
                        return await Restart();
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to run finalizer");
            }
            return false;
        }

        private async Task<bool> Start()
        {
            var isRunning = await processManager.IsRunning(Description.AppPathKey);
            if (!isRunning)
            {
                return await processManager.Start(Description.AppPathKey);
            }
            return true;
        }

        private async Task<bool> Stop()
        {
            var isRunning = await processManager.IsRunning(Description.AppPathKey);
            if (isRunning)
            {
                return await processManager.Stop(Description.AppPathKey);
            }
            return true;
        }

        private async Task<bool> Restart()
        {
            var isRunning = await processManager.IsRunning(Description.AppPathKey);
            if (isRunning)
            {
                if (!await processManager.Stop(Description.AppPathKey))
                {
                    return false;
                }
                return await processManager.Start(Description.AppPathKey);
            }
            return true;
        }
    }
}
