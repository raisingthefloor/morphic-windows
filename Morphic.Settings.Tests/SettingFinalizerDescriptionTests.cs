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

using Morphic.Core;
using Morphic.Settings.Spi;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Morphic.Settings.Tests
{
    public class SettingFinalizerDescriptionTests
    {
        [Fact]
        public void TestJsonDeserializeUnknown()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingFinalizerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "type", "invalid" }
            });
            var finalizer = JsonSerializer.Deserialize<SettingFinalizerDescription>(json, options);
            Assert.NotNull(finalizer);
            Assert.Equal(SettingFinalizerDescription.HandlerKind.Unknown, finalizer.Kind);
        }

        [Theory]
        [InlineData("setCursors", SystemParametersInfo.Action.SetCursors, null, null, true)]
        [InlineData("setCursors", SystemParametersInfo.Action.SetCursors, false, false, true)]
        [InlineData("setCursors", SystemParametersInfo.Action.SetCursors, false, true, true)]
        [InlineData("setCursors", SystemParametersInfo.Action.SetCursors, true, false, true)]
        [InlineData("setCursors", SystemParametersInfo.Action.SetCursors, true, true, true)]
        [InlineData("shenanigans", SystemParametersInfo.Action.SetCursors, false, false, false)]
        public void TestJsonDeserializeSPI(string actionString, SystemParametersInfo.Action action, bool? sendChange, bool? updateUserProfile, bool success)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingFinalizerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "type", "com.microsoft.windows.systemParametersInfo" },
                { "action", actionString },
                { "send_change", sendChange },
                { "update_user_profile", updateUserProfile }
            });
            var finalizer = JsonSerializer.Deserialize<SettingFinalizerDescription>(json, options);
            Assert.NotNull(finalizer);
            if (success)
            {
                Assert.Equal(SettingFinalizerDescription.HandlerKind.SystemParametersInfo, finalizer.Kind);
                Assert.IsType<SystemParametersInfoSettingFinalizerDescription>(finalizer);
                SystemParametersInfoSettingFinalizerDescription spi = (SystemParametersInfoSettingFinalizerDescription)finalizer;
                Assert.Equal(action, spi.Action);
                if (sendChange != null)
                    Assert.Equal(sendChange, spi.SendChange);
                if (updateUserProfile != null)
                    Assert.Equal(updateUserProfile, spi.UpdateUserProfile);
                //test equals operator
                SystemParametersInfoSettingFinalizerDescription other = new SystemParametersInfoSettingFinalizerDescription(action);
                other.SendChange = spi.SendChange;
                other.UpdateUserProfile = spi.UpdateUserProfile;
                Assert.Equal(other, spi);
            }
            else
            {
                Assert.Equal(SettingFinalizerDescription.HandlerKind.Unknown, finalizer.Kind);
            }
        }
    }
}
