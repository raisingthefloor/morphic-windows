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
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Morphic.Settings.Tests
{
    public class SettingTests
    {
        public static IEnumerable<object[]> TestData()
        {
            var handler = new Dictionary<string, object>();
            handler.Add("type", "com.microsoft.windows.registry");
            handler.Add("key_name", "thekey");
            handler.Add("value_name", "thevalue");
            handler.Add("value_type", "String");
            var finalizer = new Dictionary<string, object>();
            finalizer.Add("type", "com.microsoft.windows.systemParametersInfo");
            finalizer.Add("action", "SetCursors");
            yield return new object[] { Setting.ValueKind.String, "ayylmao", handler, finalizer, SettingHandlerDescription.HandlerKind.Registry, SettingFinalizerDescription.HandlerKind.SystemParametersInfo };
            handler = new Dictionary<string, object>();
            handler.Add("type", "com.microsoft.windows.ini");
            handler.Add("filename", "thefile");
            handler.Add("section", "thesection");
            handler.Add("key", "thekey");
            finalizer = new Dictionary<string, object>();
            finalizer.Add("type", "com.microsoft.windows.systemParametersInfo");
            finalizer.Add("action", "SetSomethingThatIsntCursors");
            yield return new object[] { Setting.ValueKind.Double, 3.14159d, handler, finalizer, SettingHandlerDescription.HandlerKind.Ini, SettingFinalizerDescription.HandlerKind.Unknown };
            handler = new Dictionary<string, object>();
            handler.Add("type", "org.raisingthefloor.morphic.client");
            handler.Add("solution", "thesolution");
            handler.Add("preference", "thepreference");
            finalizer = new Dictionary<string, object>();
            finalizer.Add("type", "com.microsoft.windows.systemParametersInfo");
            finalizer.Add("action", "SetCursors");
            yield return new object[] { Setting.ValueKind.Boolean, true, handler, finalizer, SettingHandlerDescription.HandlerKind.Client, SettingFinalizerDescription.HandlerKind.SystemParametersInfo };
            handler = new Dictionary<string, object>();
            handler.Add("type", "com.microsoft.windows.system");
            handler.Add("setting_id", "thesetting");
            finalizer = new Dictionary<string, object>();
            finalizer.Add("type", "com.microsoft.windows.systemParametersInfo");
            yield return new object[] { Setting.ValueKind.Integer, 52L, handler, finalizer, SettingHandlerDescription.HandlerKind.System, SettingFinalizerDescription.HandlerKind.Unknown };
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void TestJsonDeserialize(Setting.ValueKind kind, object defval, Dictionary<string, object> handler, Dictionary<string, object> finalizer, SettingHandlerDescription.HandlerKind handlerKind, SettingFinalizerDescription.HandlerKind finalizerKind)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());
            options.Converters.Add(new SettingFinalizerDescription.JsonConverter());

            //minimum number of fields
            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "name", "settingname" },
                { "type", kind }

            });
            var setting = JsonSerializer.Deserialize<Setting>(json, options);
            Assert.NotNull(setting);
            Assert.Equal("settingname", setting.Name);
            Assert.Equal(kind, setting.Kind);
            Assert.Null(setting.HandlerDescription);
            Assert.Null(setting.FinalizerDescription);
            Assert.Null(setting.Default);

            json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "name", "settingname" },
                { "type", kind },
                { "default", defval },
                { "handler", handler },
                { "finalizer", finalizer }
            });
            setting = JsonSerializer.Deserialize<Setting>(json, options);
            Assert.NotNull(setting);
            Assert.Equal("settingname", setting.Name);
            Assert.Equal(kind, setting.Kind);
            Assert.Equal(defval, setting.Default);
            Assert.NotNull(setting.HandlerDescription);
            Assert.Equal(handlerKind, setting.HandlerDescription.Kind);
            Assert.NotNull(setting.FinalizerDescription);
            Assert.Equal(finalizerKind, setting.FinalizerDescription.Kind);
        }
    }
}
