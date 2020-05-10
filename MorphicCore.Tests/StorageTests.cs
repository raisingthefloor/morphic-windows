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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json.Serialization;
using Xunit;
#nullable enable
#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace MorphicCore.Tests
{
    public class StorageTests
    {
        [Fact]
        public async void TestSave()
        {
            var so = new StorageOptions();
            so.RootPath = "../../../testfiles/submission";
            Storage s = new Storage(so, new NullLogger<Storage>());
            var tr = new TestResource();
            await s.Save<MockRecord>(new MockRecord());
            var a = File.ReadAllBytes("../../../testfiles/submission/MockRecord/testrecord.json");
            var b = File.ReadAllBytes("../../../testfiles/reference/MockRecord/testrecord.json");
            Assert.Equal(a, b);
            File.Delete("../../../testfiles/submission/MockRecord/testrecord.json");
        }

        [Fact]
        public async void TestLoad()
        {
            var so = new StorageOptions();
            so.RootPath = "../../../testfiles/reference";
            Storage s = new Storage(so, new NullLogger<Storage>());
            var tr = new TestResource();
            var testfile = await s.Load<MockRecord>("testrecord");
            var nofile = await s.Load<MockRecord>("thisfileisnthere");
            var badjsonfile = await s.Load<MockRecord>("badjsonfile");
            var binaryfile = await s.Load<MockRecord>("binaryfile");
            var r = tr.basicRecord;
            Assert.NotNull(testfile);
            Assert.Equal(testfile.UserId, r.UserId);
            Assert.Equal(testfile.PreferencesId, r.PreferencesId);
            Assert.Equal(testfile.FirstName, r.FirstName);
            Assert.Equal(testfile.LastName, r.LastName);
            Assert.Equal(testfile.Default.ToString(), r.Default.ToString());
            Assert.Null(nofile);
            Assert.Null(badjsonfile);
            Assert.Null(binaryfile);
        }

        [Fact]
        public void TestExists()
        {
            var so = new StorageOptions();
            so.RootPath = "../../../testfiles/reference";
            Storage s = new Storage(so, new NullLogger<Storage>());
            Assert.True(s.Exists<MockRecord>("testrecord"));
            Assert.False(s.Exists<MockRecord>("aintherechief"));
            Assert.False(s.Exists<Preferences>("testrecord"));
            Assert.True(s.Exists<Preferences>("testprefsfile"));
            Assert.False(s.Exists<MockRecord>("notajsonfile"));
        }

        //record class that contains all fields to be tested
        class MockRecord : IRecord
        {
            [JsonPropertyName("id")]
            string IRecord.Id { get; set; } = "testrecord";

            [JsonPropertyName("user_id")]
            public string? UserId { get; set; } = "62398721067952310238967627839";

            [JsonPropertyName("preferences_id")]
            public string? PreferencesId { get; set; } = "892319065789120956462348671234";

            [JsonPropertyName("first_name")]
            public string? FirstName { get; set; } = "John";

            [JsonPropertyName("last_name")]
            public string? LastName { get; set; } = "Doe";

            [JsonPropertyName("default")]
            public Dictionary<string, SolutionPreferences>? Default { get; set; } = new Dictionary<string, SolutionPreferences>();

            public MockRecord()
            {
                Default.Add("firstthing", new SolutionPreferences());
                Default["firstthing"].Values.Add("thisisastring", "ayy lmao");
                Default["firstthing"].Values.Add("thisisadouble", 3.14159d);
                Default["firstthing"].Values.Add("thisisaninteger", 52);
                Default["firstthing"].Values.Add("thisisaboolean", true);
                Dictionary<string, object?> dict = new Dictionary<string, object?>() { { "one", 1 }, { "two", 2 }, { "three", 3 } };
                Default["firstthing"].Values.Add("thisisadictionary", dict);
                object?[] arr = new object?[10];
                Default["firstthing"].Values.Add("thisisanarray", arr);
                Default.Add("secondthing", new SolutionPreferences());
                Default["secondthing"].Values.Add("thisisaboolean", false);
            }
        }

        class TestResource
        {
            public MockRecord basicRecord;
            public TestResource()
            {
                basicRecord = new MockRecord();
            }
        }
    }
}
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#nullable disable
