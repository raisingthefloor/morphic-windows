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

namespace Morphic.Settings.SystemSettings
{

    /// <summary>
    /// An interface for creating system setting instances
    /// </summary>
    /// Since system settings are created with a given id, they can't be easily created directly
    /// from a <code>ServiceProvider</code>.  So instead, the <code>ServiceProvider</code>
    /// creates a factory that knows how to create a specific kind of system setting.
    /// 
    /// Typically if you create a new kind of system setting implementation, you'll also have to
    /// create a factory for it.
    /// </remarks>
    public interface ISystemSettingFactory
    {

        /// <summary>
        /// Create a system setting for the given id
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="serviceProvider"></param>
        /// <returns></returns>
        public ISystemSetting Create(string id, IServiceProvider serviceProvider);

    }
}
