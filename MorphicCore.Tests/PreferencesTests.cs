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
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
#nullable enable
#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace MorphicCore.Tests
{
    public class PreferencesTests
    {
        [Fact]
        public void TestJsonDeserialize()
        {
            //testing fully populated
            //TODO: get default serialization working
            TestResource tr = new TestResource();
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            var pid = Guid.NewGuid().ToString();
            var uid = Guid.NewGuid().ToString();
            var json = JsonSerializer.Serialize(new Dictionary<string, object?>()
            {
                { "id", pid },
                { "user_id", uid },
                { "default", tr.Default }
            });
            var prefs = JsonSerializer.Deserialize<Preferences>(json, options);
            Assert.NotNull(prefs);
            Assert.Equal(pid, prefs.Id);
            Assert.Equal(uid, prefs.UserId);
            Assert.NotNull(prefs.Default);
            foreach(var k1 in tr.Default.Keys)
            {
                Assert.NotNull(prefs.Default[k1]);
                foreach(var k2 in tr.Default[k1].Values.Keys)
                {
                    var defval = prefs.Default[k1].Values[k2];
                    Assert.NotNull(defval);
                    Assert.Equal(tr.Default[k1].Values[k2].ToString(), defval.ToString());
                }
            }

            //testing minimally populated
            pid = Guid.NewGuid().ToString();
            json = JsonSerializer.Serialize(new Dictionary<string, object?>()
            {
                { "id", pid }
            });
            prefs = JsonSerializer.Deserialize<Preferences>(json, options);
            Assert.NotNull(prefs);
            Assert.Equal(pid, prefs.Id);

            //TODO: test invalid cases once expected behavior for deserialization failure is known
        }

        [Fact]
        public void TestJsonSerialize()
        {
            var pid = Guid.NewGuid().ToString();
            var uid = Guid.NewGuid().ToString();
            var tr = new TestResource();
            var prefs = new Preferences();
            prefs.Id = pid;
            prefs.UserId = uid;
            prefs.Default = tr.Default;
            var json = JsonSerializer.Serialize(prefs);
            var obj = JsonDocument.Parse(json).RootElement;
            Assert.Equal(pid, obj.GetProperty("id").GetString());
            Assert.Equal(uid, obj.GetProperty("user_id").GetString());
            foreach(var k1 in tr.Default.Keys)
            {
                foreach(var k2 in tr.Default[k1].Values.Keys)
                {
                    var defval = obj.GetProperty("default").GetProperty(k1).GetProperty(k2).GetObject();
                    Assert.Equal(tr.Default[k1].Values[k2].ToString(), defval.ToString());  //use toString because of object types that do not compare well
                }
            }
        }

        [Fact]
        public void TestGet()
        {
            var prefs = new Preferences();
            prefs.Default = new TestResource().Default;
            //fetch every data type
            var retstring = prefs.Get(new Preferences.Key("firstthing", "thisisastring"));
            var retdouble = prefs.Get(new Preferences.Key("firstthing", "thisisadouble"));
            var retint = prefs.Get(new Preferences.Key("firstthing", "thisisaninteger"));
            var retbool = prefs.Get(new Preferences.Key("firstthing", "thisisaboolean"));
            var retdict = prefs.Get(new Preferences.Key("firstthing", "thisisadictionary"));
            var retarr = prefs.Get(new Preferences.Key("firstthing", "thisisanarray"));
            //try to fetch something that isn't there, and something in a different solution
            var nothere = prefs.Get(new Preferences.Key("firstthing", "somethingdifferent"));
            var wrongplace = prefs.Get(new Preferences.Key("secondthing", "thisisadictionary"));

            Assert.IsType<string>(retstring);
            Assert.Equal(retstring, prefs.Default["firstthing"].Values["thisisastring"]);
            Assert.IsType<Double>(retdouble);
            Assert.Equal(retdouble, prefs.Default["firstthing"].Values["thisisadouble"]);
            Assert.IsType<int>(retint);
            Assert.Equal(retint, prefs.Default["firstthing"].Values["thisisaninteger"]);
            Assert.IsType<Boolean>(retbool);
            Assert.Equal(retbool, prefs.Default["firstthing"].Values["thisisaboolean"]);
            Assert.IsType<Dictionary<string, object?>>(retdict);
            Assert.Equal(retdict, prefs.Default["firstthing"].Values["thisisadictionary"]);
            Assert.IsType<Object?[]>(retarr);
            Assert.Equal(retarr, prefs.Default["firstthing"].Values["thisisanarray"]);
            Assert.Null(nothere);
            Assert.Null(wrongplace);
        }

        [Fact]
        public void TestSet()
        {
            var prefs = new Preferences();
            var tr = new TestResource();
            //prefs.Default = new tResource1().Default;
            prefs.Set(new Preferences.Key("firstthing", "thisisastring"), "set the string with a different value to start");
            prefs.Set(new Preferences.Key("firstthing", "thisisadouble"), 3.14159d);
            prefs.Set(new Preferences.Key("firstthing", "thisisaninteger"), "whoops I used the wrong data type");
            prefs.Set(new Preferences.Key("firstthing", "thisisaninteger"), 12345);
            prefs.Set(new Preferences.Key("firstthing", "thisisaboolean"), true);
            prefs.Set(new Preferences.Key("firstthing", "thisisadictionary"), 823847);
            prefs.Set(new Preferences.Key("firstthing", "thisisadictionary"), tr.Default["firstthing"].Values["thisisadictionary"]);
            prefs.Set(new Preferences.Key("firstthing", "thisisanarray"), new object?[40]);
            prefs.Set(new Preferences.Key("firstthing", "thisisastring"), "now change the string");

            Assert.IsType<string>(prefs.Default["firstthing"].Values["thisisastring"]);
            Assert.Equal("now change the string", prefs.Default["firstthing"].Values["thisisastring"]);
            Assert.IsType<Double>(prefs.Default["firstthing"].Values["thisisadouble"]);
            Assert.Equal(3.14159d, prefs.Default["firstthing"].Values["thisisadouble"]);
            Assert.IsType<int>(prefs.Default["firstthing"].Values["thisisaninteger"]);
            Assert.Equal(12345, prefs.Default["firstthing"].Values["thisisaninteger"]);
            Assert.IsType<Boolean>(prefs.Default["firstthing"].Values["thisisaboolean"]);
            Assert.Equal(true, prefs.Default["firstthing"].Values["thisisaboolean"]);
            Assert.IsType<Dictionary<string, object?>>(prefs.Default["firstthing"].Values["thisisadictionary"]);
            Assert.Equal(tr.Default["firstthing"].Values["thisisadictionary"], prefs.Default["firstthing"].Values["thisisadictionary"]);
            Assert.IsType<Object?[]>(prefs.Default["firstthing"].Values["thisisanarray"]);
            Assert.Equal(new object?[40], prefs.Default["firstthing"].Values["thisisanarray"]);
        }

        //test resources

        class TestResource
        {
            public Dictionary<string, SolutionPreferences> Default;
            public string serialized;
            public TestResource()
            {
                Default = new Dictionary<string, SolutionPreferences>();
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
                var options = new JsonSerializerOptions();
                options.Converters.Add(new JsonElementInferredTypeConverter());
                serialized = JsonSerializer.Serialize(Default, options);
            }
        }
    }
}
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#nullable disable
