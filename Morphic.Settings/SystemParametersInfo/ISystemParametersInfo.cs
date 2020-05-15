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

namespace Morphic.Settings.Spi
{

    /// <summary>
    /// An interface for System Parameter Info (SPI) calls
    /// </summary>
    public interface ISystemParametersInfo
    {

        /// <summary>
        /// Call SystemParametersInfo to set a value or perform an action
        /// </summary>
        /// <param name="action"></param>
        /// <param name="parameter1"></param>
        /// <param name="parameter2"></param>
        /// <param name="updateUserProfile"></param>
        /// <param name="sendChange"></param>
        /// <returns></returns>
        public bool Call(SystemParametersInfo.Action action, int parameter1, object? parameter2, bool updateUserProfile = false, bool sendChange = false);

    }
}
