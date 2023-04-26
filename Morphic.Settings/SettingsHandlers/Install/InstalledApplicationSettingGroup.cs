// Copyright 2023 Raising the Floor - US, Inc.
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Settings.SettingsHandlers.Install
{
    using Newtonsoft.Json;

    [SettingsHandlerType("installedApplication", typeof(InstalledApplicationSettingsHandler))]
    public class InstalledApplicationSettingGroup : SettingGroup
    {
        /// <summary>The short name used to identify the application (including multiple installers as a group) by AT on Demand and Morphic.</summary>
        [JsonProperty("shortName", Required = Required.Always)]
        public string ShortName { get; set; } = null!;

        /// <summary>The product code GUID used by Windows to identify that an application (or MSI package) is installed.</summary>
        [JsonProperty("productCode", Required = Required.Always)]
        public string ProductCode { get; set; } = null!;
    }
}
