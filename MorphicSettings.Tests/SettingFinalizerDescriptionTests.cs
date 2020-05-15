using MorphicCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Xunit;

namespace MorphicSettings.Tests
{
    public class SettingFinalizerDescriptionTests
    {
        [Fact]
        public void TestJsonDeserializeUnknown()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingFinalizerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object?>()
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

            var json = JsonSerializer.Serialize(new Dictionary<string, object?>()
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
            }
            else
            {
                Assert.Equal(SettingFinalizerDescription.HandlerKind.Unknown, finalizer.Kind);
            }
        }
    }
}
