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
using System.Text.Json;
using System.Collections.Generic;
using Xunit;
using Microsoft.Extensions.Options;
#nullable enable

namespace MorphicCore.Tests
{
    public class UserTests
    {
        [Fact]
        public void TestJsonDeserialize()
        {
            // Valid user, all fields populated
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            var uid = Guid.NewGuid().ToString();
            var pid = Guid.NewGuid().ToString();
            var json = JsonSerializer.Serialize(new Dictionary<string, object?>()
            {
                { "id", uid },
                { "preferences_id", pid },
                { "first_name", "John" },
                { "last_name", "Doe" }
            });
            var user = JsonSerializer.Deserialize<User>(json, options);
            Assert.NotNull(user);
            Assert.Equal(uid, user.Id);
            Assert.Equal(pid, user.PreferencesId);
            Assert.Equal("John", user.FirstName);
            Assert.Equal("Doe", user.LastName);


            // Valid user, minimum set of fields populated
            uid = Guid.NewGuid().ToString();
            json = JsonSerializer.Serialize(new Dictionary<string, object?>()
            {
                { "id", uid }
            });
            user = JsonSerializer.Deserialize<User>(json, options);
            Assert.NotNull(user);
            Assert.Equal(uid, user.Id);
            Assert.Null(user.PreferencesId);
            Assert.Null(user.FirstName);
            Assert.Null(user.LastName);


            // Invalid user, all other fields populated
            pid = Guid.NewGuid().ToString();
            json = JsonSerializer.Serialize(new Dictionary<string, object?>()
            {
                { "preferences_id", pid },
                { "first_name", "John" },
                { "last_name", "Doe" }
            });
            user = JsonSerializer.Deserialize<User>(json, options);
            Assert.NotNull(user);
            //TODO: this one should actually fail, need to change the code
        }

        [Fact]
        public void TestJsonSerialize()
        {
            var uid = Guid.NewGuid().ToString();
            var pid = Guid.NewGuid().ToString();
            User user = new User();
            user.Id = uid;
            user.PreferencesId = pid;
            user.FirstName = "John";
            user.LastName = "Doe";
            var json = JsonSerializer.Serialize(user);
            var obj = JsonDocument.Parse(json).RootElement;
            Assert.Equal(uid, obj.GetProperty("id").GetString());
            Assert.Equal(pid, obj.GetProperty("preferences_id").GetString());
            Assert.Equal("John", obj.GetProperty("first_name").GetString());
            Assert.Equal("Doe", obj.GetProperty("last_name").GetString());
        }
    }
}
#nullable disable