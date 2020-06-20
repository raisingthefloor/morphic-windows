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
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Morphic.Settings.Process
{
    public class ProcessSettingFinalizerDescription: SettingFinalizerDescription
    {

        public ProcessSettingFinalizerDescription(string exe, ProcessAction action): base(FinalizerKind.Process)
        {
            Exe = exe;
            Action = action;
        }

        public ProcessSettingFinalizerDescription(JsonElement element): base(FinalizerKind.Process)
        {
            Exe = element.GetProperty("exe").GetString();
            if (Exe == null)
            {
                throw new JsonException();
            }
            var actionString = element.GetProperty("action").GetString();
            Action = Enum.Parse<ProcessAction>(actionString, ignoreCase: true);
        }

        public string Exe { get; }

        public ProcessAction Action;

        public override bool Equals(object? obj)
        {
            if (obj is ProcessSettingFinalizerDescription other)
            {
                return Exe == other.Exe && Action == other.Action;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Exe.GetHashCode() ^ Action.GetHashCode();
        }

    }
}
