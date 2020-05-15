using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Morphic.Settings.Tests
{
    public class SolutionTests
    {
        public SolutionTests()
        {
            msettings = new Dictionary<string, object?>[5];
            msettings[0] = new Dictionary<string, object?>();
            msettings[0]["name"] = "thisisastring";
            msettings[0]["type"] = Setting.ValueKind.String;
            msettings[0]["default"] = "ayylmao";
            msettings[1] = new Dictionary<string, object?>();
            msettings[1]["name"] = "thisisaboolean";
            msettings[1]["type"] = Setting.ValueKind.Boolean;
            msettings[1]["default"] = true;
            msettings[2] = new Dictionary<string, object?>();
            msettings[2]["name"] = "thisisanint";
            msettings[2]["type"] = Setting.ValueKind.Integer;
            msettings[2]["default"] = 12345L;
            msettings[3] = new Dictionary<string, object?>();
            msettings[3]["name"] = "thisisadouble";
            msettings[3]["type"] = Setting.ValueKind.Double;
            msettings[3]["default"] = 3.14159d;
            msettings[4] = new Dictionary<string, object?>();
            msettings[4]["name"] = "thisisnull";
            msettings[4]["type"] = Setting.ValueKind.String;
            msettings[4]["default"] = null;
        }

        private Dictionary<string, object?>[] msettings;

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
            Assert.NotNull(solution.Settings[0]);
            Assert.Equal(msettings[0]["default"], solution.Settings[0].Default);
            Assert.NotNull(solution.Settings[1]);
            Assert.Equal(msettings[1]["default"], solution.Settings[1].Default);
            Assert.NotNull(solution.Settings[2]);
            Assert.Equal(msettings[2]["default"], solution.Settings[2].Default);
            Assert.NotNull(solution.Settings[3]);
            Assert.Equal(msettings[3]["default"], solution.Settings[3].Default);
            Assert.NotNull(solution.Settings[4]);
            Assert.Null(msettings[4]["default"]);
            //TEST SETTINGSBYNAME
            Assert.NotNull(solution.SettingsByName["thisisastring"]);
            Assert.Equal(msettings[0]["default"], solution.SettingsByName["thisisastring"].Default);
            Assert.NotNull(solution.SettingsByName["thisisaboolean"]);
            Assert.Equal(msettings[1]["default"], solution.SettingsByName["thisisaboolean"].Default);
            Assert.NotNull(solution.SettingsByName["thisisanint"]);
            Assert.Equal(msettings[2]["default"], solution.SettingsByName["thisisanint"].Default);
            Assert.NotNull(solution.SettingsByName["thisisadouble"]);
            Assert.Equal(msettings[3]["default"], solution.SettingsByName["thisisadouble"].Default);
            Assert.NotNull(solution.SettingsByName["thisisnull"]);
            Assert.Null(solution.SettingsByName["thisisnull"].Default);
        }
    }
}
