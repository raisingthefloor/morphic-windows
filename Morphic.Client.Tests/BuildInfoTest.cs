using Morphic.Client.About;
using Morphic.Core;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Morphic.Client.Tests
{
    public class BuildInfoTest
    {

        [Theory]
        [InlineData("astring", "bstring")]
        [InlineData("!!!!!!!!!", "??????????")]
        public void TestJsonDeserialize(string buildtime, string commit)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            var json = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                {"buildTime", buildtime },
                {"commit", commit }
            });
            var info = JsonSerializer.Deserialize<BuildInfo>(json, options);
            Assert.NotNull(info);
            Assert.NotNull(info.BuildTime);
            Assert.Equal(buildtime, info.BuildTime);
            Assert.NotNull(info.Commit);
            Assert.Equal(commit, info.Commit);
        }
    }
}
