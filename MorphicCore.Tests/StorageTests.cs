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
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

namespace MorphicCore.Tests
{
    public class StorageTests : IDisposable
    {
        public StorageTests()
        {
            directoryName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            byte[] junk = new byte[100];
            Random rnd = new Random();
            rnd.NextBytes(junk);
            Directory.CreateDirectory(Path.Combine(directoryName, "MockRecord"));
            File.WriteAllBytes(Path.Combine(directoryName, "MockRecord/binaryfile.json"), junk);
            File.WriteAllText(Path.Combine(directoryName, "MockRecord/badjsonfile.json"), "{\"whoops\"; \"thisisafail\"}");
            File.WriteAllText(Path.Combine(directoryName, "MockRecord/incorrectfields.json"), "{\"notarecord\": \"atall\"}");
            File.WriteAllText(Path.Combine(directoryName, "MockRecord/notajsonfile"), "{\"ayy\":\"lmao\"}");
        }


        private string directoryName;

        [Fact]
        public async void TestSaveLoad()
        {
            var so = new StorageOptions();
            so.RootPath = directoryName;
            var logger = new LoggerFactory().CreateLogger<Storage>();
            //SAVING TEST
            Storage s = new Storage(so, logger);
            var mock = new MockRecord();
            mock.populate();
            bool sav = await s.Save<MockRecord>(mock);
            Assert.True(sav);
            //EXISTS TEST
            Assert.True(s.Exists<MockRecord>("testrecord"));
            Assert.True(s.Exists<MockRecord>("binaryfile"));
            Assert.True(s.Exists<MockRecord>("badjsonfile"));
            Assert.True(s.Exists<MockRecord>("incorrectfields"));
            Assert.False(s.Exists<MockRecord>("aintherechief"));
            Assert.False(s.Exists<Preferences>("testrecord"));
            Assert.False(s.Exists<MockRecord>("notajsonfile"));
            //LOAD TEST
            var testfile = await s.Load<MockRecord>("testrecord");
            var nofile = await s.Load<MockRecord>("thisfileisnthere");
            var wrongfields = await s.Load<MockRecord>("incorrectfields");
            var badjsonfile = await s.Load<MockRecord>("badjsonfile");
            var notajson = await s.Load<MockRecord>("notajsonfile");
            var binaryfile = await s.Load<MockRecord>("binaryfile");
            Assert.NotNull(testfile);
            Assert.Equal(mock.UserId, testfile.UserId);
            Assert.Equal(mock.PreferencesId, testfile.PreferencesId);
            Assert.Equal(mock.FirstName, testfile.FirstName);
            Assert.Equal(mock.LastName, testfile.LastName);
            Assert.Equal(mock.Default["firstthing"].Values["thisisastring"], testfile.Default["firstthing"].Values["thisisastring"]);
            Assert.Equal(mock.Default["firstthing"].Values["thisisadouble"], testfile.Default["firstthing"].Values["thisisadouble"]);
            Assert.Equal(mock.Default["firstthing"].Values["thisisaninteger"], testfile.Default["firstthing"].Values["thisisaninteger"]);
            Assert.Equal(mock.Default["firstthing"].Values["thisisaboolean"], testfile.Default["firstthing"].Values["thisisaboolean"]);
            Assert.Null(nofile);
            //Assert.Null(wrongfields); //TODO: assess whether check should be done as to whether proper fields are provided?
            Assert.Null(badjsonfile);
            Assert.Null(notajson);
            Assert.Null(binaryfile);
        }

        private string GetTestPath(string file)
        {
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\testfiles\\storage", file));
        }

        //record class that contains all fields to be tested
        class MockRecord : IRecord
        {
            [JsonPropertyName("id")]
            string IRecord.Id { get; set; } = "testrecord";

            [JsonPropertyName("user_id")]
            public string? UserId { get; set; }

            [JsonPropertyName("preferences_id")]
            public string? PreferencesId { get; set; }

            [JsonPropertyName("first_name")]
            public string? FirstName { get; set; }

            [JsonPropertyName("last_name")]
            public string? LastName { get; set; }

            [JsonPropertyName("default")]
            public Dictionary<string, SolutionPreferences>? Default { get; set; }

            public void populate()
            {
                UserId = "62398721067952310238967627839";
                PreferencesId = "892319065789120956462348671234";
                FirstName = "John";
                LastName = "Doe";
                Default = new Dictionary<string, SolutionPreferences>();
                Default.Add("firstthing", new SolutionPreferences());
                Default["firstthing"].Values.Add("thisisastring", "ayy lmao");
                Default["firstthing"].Values.Add("thisisadouble", 3.14159d);
                Default["firstthing"].Values.Add("thisisaninteger", 52L);
                Default["firstthing"].Values.Add("thisisaboolean", true);
                Dictionary<string, object?> dict = new Dictionary<string, object?>() { { "one", 1 }, { "two", 2 }, { "three", 3 } };
                Default["firstthing"].Values.Add("thisisadictionary", dict);
                object?[] arr = new object?[10];
                Default["firstthing"].Values.Add("thisisanarray", arr);
                Default.Add("secondthing", new SolutionPreferences());
                Default["secondthing"].Values.Add("thisisaboolean", false);
            }
        }

        public void Dispose()
        {
            Directory.Delete(directoryName, true);
        }
    }
}

#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
