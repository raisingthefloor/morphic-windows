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
using Morphic.Settings.Process;
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
            Assert.Equal(SettingFinalizerDescription.FinalizerKind.Unknown, finalizer.Kind);
        }

        [Theory]
        [InlineData("setcursors", SystemParametersInfo.Action.SetCursors, null, null, false, false)]
        [InlineData("SeTcUrSoRs", SystemParametersInfo.Action.SetCursors, null, null, false, false)]
        [InlineData("sEtCuRsOrS", SystemParametersInfo.Action.SetCursors, null, null, false, false)]
        [InlineData("SETCURSORS", SystemParametersInfo.Action.SetCursors, null, null, false, false)]
        [InlineData("setCursors", SystemParametersInfo.Action.SetCursors, false, false, false, false)]
        [InlineData("setCursors", SystemParametersInfo.Action.SetCursors, false, true, false, true)]
        [InlineData("setCursors", SystemParametersInfo.Action.SetCursors, true, false, true, false)]
        [InlineData("setCursors", SystemParametersInfo.Action.SetCursors, true, true, true, true)]
        public void TestJsonDeserializeSPI(string actionString, SystemParametersInfo.Action action, bool? sendChange, bool? updateUserProfile, bool expectedSendChange, bool expectedUpdateUserProfile)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingFinalizerDescription.JsonConverter());

            var obj = new Dictionary<string, object>()
            {
                { "type", "com.microsoft.windows.systemParametersInfo" },
                { "action", actionString }
            };
            if (sendChange is bool sendChangeBool)
            {
                obj.Add("send_change", sendChangeBool);
            }
            if (updateUserProfile is bool updateUserProfileBool)
            {
                obj.Add("update_user_profile", updateUserProfileBool);
            }
            var json = JsonSerializer.Serialize(obj);
            var finalizer = JsonSerializer.Deserialize<SettingFinalizerDescription>(json, options);
            Assert.NotNull(finalizer);
            Assert.Equal(SettingFinalizerDescription.FinalizerKind.SystemParametersInfo, finalizer.Kind);
            Assert.IsType<SystemParametersInfoSettingFinalizerDescription>(finalizer);
            SystemParametersInfoSettingFinalizerDescription spi = (SystemParametersInfoSettingFinalizerDescription)finalizer;
            Assert.Equal(action, spi.Action);
            Assert.Equal(expectedSendChange, spi.SendChange);
            Assert.Equal(expectedUpdateUserProfile, spi.UpdateUserProfile);
        }

        [Theory]
        [InlineData("shenanigans")]
        [InlineData(null)]
        [InlineData(1)]
        [InlineData(1.2)]
        [InlineData(true)]
        public void TestJsonDeserializeSPIFail(object actionString)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingFinalizerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "type", "com.microsoft.windows.systemParametersInfo" },
                { "action", actionString }
            });
            var finalizer = JsonSerializer.Deserialize<SettingFinalizerDescription>(json, options);
            Assert.NotNull(finalizer);
            Assert.Equal(SettingFinalizerDescription.FinalizerKind.Unknown, finalizer.Kind);
        }

        [Fact]
        public void TestEqualSPI()
        {
            var description1 = new SystemParametersInfoSettingFinalizerDescription(SystemParametersInfo.Action.SetCursors);
            var description2 = new SystemParametersInfoSettingFinalizerDescription(SystemParametersInfo.Action.SetCursors);
            Assert.Equal(description1, description2);

            description1 = new SystemParametersInfoSettingFinalizerDescription(SystemParametersInfo.Action.SetCursors);
            description2 = new SystemParametersInfoSettingFinalizerDescription(SystemParametersInfo.Action.SetCursors);
            description2.Parameter1 = 1;
            Assert.NotEqual(description1, description2);
            description1.Parameter1 = 1;
            Assert.Equal(description1, description2);

            description1 = new SystemParametersInfoSettingFinalizerDescription(SystemParametersInfo.Action.SetCursors);
            description2 = new SystemParametersInfoSettingFinalizerDescription(SystemParametersInfo.Action.SetCursors);
            description2.Parameter2 = new object();
            Assert.NotEqual(description1, description2);
            description1.Parameter2 = description2.Parameter2;
            Assert.Equal(description1, description2);

            description1 = new SystemParametersInfoSettingFinalizerDescription(SystemParametersInfo.Action.SetCursors);
            description2 = new SystemParametersInfoSettingFinalizerDescription(SystemParametersInfo.Action.SetCursors);
            description2.UpdateUserProfile = true;
            Assert.NotEqual(description1, description2);
            description1.UpdateUserProfile = true;
            Assert.Equal(description1, description2);

            description1 = new SystemParametersInfoSettingFinalizerDescription(SystemParametersInfo.Action.SetCursors);
            description2 = new SystemParametersInfoSettingFinalizerDescription(SystemParametersInfo.Action.SetCursors);
            description2.SendChange = true;
            Assert.NotEqual(description1, description2);
            description1.SendChange = true;
            Assert.Equal(description1, description2);
        }

        [Theory]
        [InlineData("test.exe", "start", ProcessAction.Start)]
        [InlineData("test.exe", "stop", ProcessAction.Stop)]
        [InlineData("test.exe", "restart", ProcessAction.Restart)]
        [InlineData("test.exe", "Start", ProcessAction.Start)]
        [InlineData("test.exe", "START", ProcessAction.Start)]
        [InlineData("test.exe", "StArT", ProcessAction.Start)]
        public void TestJsonDeserializeProcess(string exe, string actionString, ProcessAction action)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingFinalizerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "type", "com.microsoft.windows.process" },
                { "exe", exe },
                { "action", actionString }
            });
            var finalizer = JsonSerializer.Deserialize<SettingFinalizerDescription>(json, options);
            Assert.NotNull(finalizer);
            Assert.Equal(SettingFinalizerDescription.FinalizerKind.Process, finalizer.Kind);
            Assert.IsType<ProcessSettingFinalizerDescription>(finalizer);
            var process = (ProcessSettingFinalizerDescription)finalizer;
            Assert.Equal(exe, process.Exe);
            Assert.Equal(action, process.Action);
        }

        [Theory]
        [InlineData("test.exe", "bad")]
        [InlineData("test.exe", null)]
        [InlineData("test.exe", 1)]
        [InlineData("test.exe", 1.2)]
        [InlineData("test.exe", true)]
        [InlineData(null, "start")]
        [InlineData(1, "start")]
        [InlineData(1.2, "start")]
        [InlineData(true, "start")]
        public void TestJsonDeserializeProcessFail(object exe, object actionString)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingFinalizerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "type", "com.microsoft.windows.process" },
                { "exe", exe },
                { "action", actionString }
            });
            var finalizer = JsonSerializer.Deserialize<SettingFinalizerDescription>(json, options);
            Assert.NotNull(finalizer);
            Assert.Equal(SettingFinalizerDescription.FinalizerKind.Unknown, finalizer.Kind);
        }

        [Fact]
        public void TestEqualProcess()
        {
            var description1 = new ProcessSettingFinalizerDescription("test.exe", ProcessAction.Start);
            var description2 = new ProcessSettingFinalizerDescription("test.exe", ProcessAction.Start);
            Assert.Equal(description1, description2);

            description1 = new ProcessSettingFinalizerDescription("test.exe", ProcessAction.Start);
            description2 = new ProcessSettingFinalizerDescription("test.exe", ProcessAction.Stop);
            Assert.NotEqual(description1, description2);

            description1 = new ProcessSettingFinalizerDescription("test.exe", ProcessAction.Start);
            description2 = new ProcessSettingFinalizerDescription("test2.exe", ProcessAction.Start);
            Assert.NotEqual(description1, description2);

            description1 = new ProcessSettingFinalizerDescription("test.exe", ProcessAction.Stop);
            description2 = new ProcessSettingFinalizerDescription("test.exe", ProcessAction.Stop);
            Assert.Equal(description1, description2);

            description1 = new ProcessSettingFinalizerDescription("test.exe", ProcessAction.Restart);
            description2 = new ProcessSettingFinalizerDescription("test.exe", ProcessAction.Restart);
            Assert.Equal(description1, description2);
        }
    }
}
