using MorphicCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Xunit;

namespace MorphicSettings.Tests
{
    public class SolutionTests
    {
        public SolutionTests()
        {
            msettings = new Setting[4];
            msettings[0] = new Setting();
            msettings[0].Name = "thisisastring";
            msettings[0].Kind = Setting.ValueKind.String;
            msettings[0].Default = "ayylmao";
            msettings[1] = new Setting();
            msettings[1].Name = "thisisaboolean";
            msettings[1].Kind = Setting.ValueKind.Boolean;
            msettings[1].Default = true;
            msettings[2] = new Setting();
            msettings[2].Name = "thisisanint";
            msettings[2].Kind = Setting.ValueKind.Integer;
            msettings[2].Default = 12345L;
            msettings[3] = new Setting();
            msettings[3].Name = "thisisadouble";
            msettings[3].Kind = Setting.ValueKind.Double;
            msettings[3].Default = 3.14159d;
        }

        private Setting[] msettings;

        [Fact]
        public void TestJsonDeserialize()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            var id = Guid.NewGuid().ToString();
            var json = JsonSerializer.Serialize(new Dictionary<string, object?>()
            {
                { "id", id },
                { "settings", msettings }
            });
            var solution = JsonSerializer.Deserialize<Solution>(json, options);
            Assert.NotNull(solution);
            Assert.Equal(id, solution.Id);
            Assert.NotNull(solution.SettingsByName["thisisastring"]);
            Assert.Equal(msettings[0].Default, solution.SettingsByName["thisisastring"].Default);
            Assert.NotNull(solution.SettingsByName["thisisaboolean"]);
            Assert.Equal(msettings[1].Default, solution.SettingsByName["thisisaboolean"].Default);
            Assert.NotNull(solution.SettingsByName["thisisanint"]);
            Assert.Equal(msettings[2].Default, solution.SettingsByName["thisisanint"].Default);
            Assert.NotNull(solution.SettingsByName["thisisadouble"]);
            Assert.Equal(msettings[3].Default, solution.SettingsByName["thisisadouble"].Default);
        }
    }
}
