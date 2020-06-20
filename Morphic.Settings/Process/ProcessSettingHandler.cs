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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Settings.Process
{
    public class ProcessSettingHandler : SettingHandler
    {

        public ProcessSettingHandler(Setting setting, IProcessManager processManager, ILogger<ProcessSettingHandler> logger)
        {
            Setting = setting;
            this.processManager = processManager;
            this.logger = logger;
        }

        public Setting Setting { get; }

        public ProcessSettingHandlerDescription Description
        {
            get
            {
                return (Setting.HandlerDescription as ProcessSettingHandlerDescription)!;
            }
        }

        private readonly IProcessManager processManager;

        private readonly ILogger<ProcessSettingHandler> logger;

        public override async Task<bool> Apply(object? value)
        {
            switch (Description.State)
            {
                case ProcessState.Running:
                    return await SetRunning(value);
            }
            return false;
        }

        public async Task<bool> SetRunning(object? value)
        {
            if (value is bool isRunning)
            {
                try
                {
                    if (isRunning)
                    {
                        if (!await processManager.IsRunning(Description.Exe))
                        {
                            return await processManager.Start(Description.Exe);
                        }
                        return true;
                    }
                    if (await processManager.IsRunning(Description.Exe))
                    {
                        return await processManager.Stop(Description.Exe);
                    }
                    return true;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to start process");
                }
            }
            return false;
        }

        public override async Task<CaptureResult> Capture()
        {
            var result = new CaptureResult();
            try
            {
                switch (Description.State)
                {
                    case ProcessState.Running:
                        result.Value = await processManager.IsRunning(Description.Exe);
                        break;
                    case ProcessState.Installed:
                        result.Value = await processManager.IsInstalled(Description.Exe);
                        break;
                }
                result.Success = true;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to check process status");
            }
            return result;
        }
    }
}
