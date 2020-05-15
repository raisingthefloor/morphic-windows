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

namespace Morphic.Core.Tests
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
            var preferencesid = Guid.NewGuid().ToString();
            var userid = Guid.NewGuid().ToString();
            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "id", preferencesid },
                { "user_id", userid },
                { "default", tr.Default }
            });
            var preferences = JsonSerializer.Deserialize<Preferences>(json, options);
            Assert.NotNull(preferences);
            Assert.Equal(preferencesid, preferences.Id);
            Assert.Equal(userid, preferences.UserId);
            Assert.NotNull(preferences.Default);
            Assert.Equal("ayy lmao", preferences.Default["firstthing"].Values["thisisastring"]);
            Assert.Equal(3.14159d, preferences.Default["firstthing"].Values["thisisadouble"]);
            Assert.Equal(52L, preferences.Default["firstthing"].Values["thisisaninteger"]);
            Assert.Equal(true, preferences.Default["firstthing"].Values["thisisaboolean"]);
            var dictionary = (Dictionary<string, object>)preferences.Default["firstthing"].Values["thisisadictionary"];
            Assert.Equal(1L, dictionary["one"]);
            Assert.Equal(2L, dictionary["two"]);
            Assert.Equal(3L, dictionary["three"]);
            Assert.Equal(413L, ((object[])preferences.Default["firstthing"].Values["thisisanarray"])[5]);

            //testing minimally populated
            preferencesid = Guid.NewGuid().ToString();
            json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "id", preferencesid }
            });
            preferences = JsonSerializer.Deserialize<Preferences>(json, options);
            Assert.NotNull(preferences);
            Assert.Equal(preferencesid, preferences.Id);

            //TODO: test invalid cases once expected behavior for deserialization failure is known
        }

        [Fact]
        public void TestJsonSerialize()
        {
            var preferencesid = Guid.NewGuid().ToString();
            var userid = Guid.NewGuid().ToString();
            var resource = new TestResource();
            var preferences = new Preferences();
            preferences.Id = preferencesid;
            preferences.UserId = userid;
            preferences.Default = resource.Default;
            var json = JsonSerializer.Serialize(preferences);
            var jsonobject = JsonDocument.Parse(json).RootElement;
            Assert.Equal(preferencesid, jsonobject.GetProperty("id").GetString());
            Assert.Equal(userid, jsonobject.GetProperty("user_id").GetString());
            Assert.Equal("ayy lmao", jsonobject.GetProperty("default").GetProperty("firstthing").GetProperty("thisisastring").GetString());
            Assert.Equal(3.14159d, jsonobject.GetProperty("default").GetProperty("firstthing").GetProperty("thisisadouble").GetDouble());
            Assert.Equal(52L, jsonobject.GetProperty("default").GetProperty("firstthing").GetProperty("thisisaninteger").GetInt64());
            Assert.True(jsonobject.GetProperty("default").GetProperty("firstthing").GetProperty("thisisaboolean").GetBoolean());
            Assert.Equal(1L, jsonobject.GetProperty("default").GetProperty("firstthing").GetProperty("thisisadictionary").GetProperty("one").GetInt64());
            Assert.Equal(2L, jsonobject.GetProperty("default").GetProperty("firstthing").GetProperty("thisisadictionary").GetProperty("two").GetInt64());
            Assert.Equal(3L, jsonobject.GetProperty("default").GetProperty("firstthing").GetProperty("thisisadictionary").GetProperty("three").GetInt64());
            Assert.Equal(413L, jsonobject.GetProperty("default").GetProperty("firstthing").GetProperty("thisisanarray")[5].GetInt64());
        }

        [Fact]
        public void TestGet()
        {
            var preferences = new Preferences();
            preferences.Default = new TestResource().Default;
            //fetch every data type
            var returnstring = preferences.Get(new Preferences.Key("firstthing", "thisisastring"));
            var returndouble = preferences.Get(new Preferences.Key("firstthing", "thisisadouble"));
            var returnint = preferences.Get(new Preferences.Key("firstthing", "thisisaninteger"));
            var returnboolean = preferences.Get(new Preferences.Key("firstthing", "thisisaboolean"));
            var returndictionary = preferences.Get(new Preferences.Key("firstthing", "thisisadictionary"));
            var returnarray = preferences.Get(new Preferences.Key("firstthing", "thisisanarray"));
            //try to fetch something that isn't there, and something in a different solution
            var nothere = preferences.Get(new Preferences.Key("firstthing", "somethingdifferent"));
            var wrongplace = preferences.Get(new Preferences.Key("secondthing", "thisisadictionary"));

            Assert.IsType<string>(returnstring);
            Assert.Equal(returnstring, preferences.Default["firstthing"].Values["thisisastring"]);
            Assert.IsType<Double>(returndouble);
            Assert.Equal(returndouble, preferences.Default["firstthing"].Values["thisisadouble"]);
            Assert.IsType<Int64>(returnint);
            Assert.Equal(returnint, preferences.Default["firstthing"].Values["thisisaninteger"]);
            Assert.IsType<Boolean>(returnboolean);
            Assert.Equal(returnboolean, preferences.Default["firstthing"].Values["thisisaboolean"]);
            Assert.IsType<Dictionary<string, object>>(returndictionary);
            Assert.Equal(returndictionary, preferences.Default["firstthing"].Values["thisisadictionary"]);
            Assert.IsType<object[]>(returnarray);
            Assert.Equal(returnarray, preferences.Default["firstthing"].Values["thisisanarray"]);
            Assert.Null(nothere);
            Assert.Null(wrongplace);
        }

        [Fact]
        public void TestSet()
        {
            var preferences = new Preferences();
            var resource = new TestResource();
            //prefs.Default = new tResource1().Default;
            preferences.Set(new Preferences.Key("firstthing", "thisisastring"), "set the string with a different value to start");
            preferences.Set(new Preferences.Key("firstthing", "thisisadouble"), 3.14159d);
            preferences.Set(new Preferences.Key("firstthing", "thisisaninteger"), "whoops I used the wrong data type");
            preferences.Set(new Preferences.Key("firstthing", "thisisaninteger"), 12345L);
            preferences.Set(new Preferences.Key("firstthing", "thisisaboolean"), true);
            preferences.Set(new Preferences.Key("firstthing", "thisisadictionary"), 823847L);
            preferences.Set(new Preferences.Key("firstthing", "thisisadictionary"), resource.Default["firstthing"].Values["thisisadictionary"]);
            preferences.Set(new Preferences.Key("firstthing", "thisisanarray"), new object[40]);
            preferences.Set(new Preferences.Key("firstthing", "thisisastring"), "now change the string");

            Assert.IsType<string>(preferences.Default["firstthing"].Values["thisisastring"]);
            Assert.Equal("now change the string", preferences.Default["firstthing"].Values["thisisastring"]);
            Assert.IsType<Double>(preferences.Default["firstthing"].Values["thisisadouble"]);
            Assert.Equal(3.14159d, preferences.Default["firstthing"].Values["thisisadouble"]);
            Assert.IsType<Int64>(preferences.Default["firstthing"].Values["thisisaninteger"]);
            Assert.Equal(12345L, preferences.Default["firstthing"].Values["thisisaninteger"]);
            Assert.IsType<Boolean>(preferences.Default["firstthing"].Values["thisisaboolean"]);
            Assert.Equal(true, preferences.Default["firstthing"].Values["thisisaboolean"]);
            Assert.IsType<Dictionary<string, object>>(preferences.Default["firstthing"].Values["thisisadictionary"]);
            Assert.Equal(resource.Default["firstthing"].Values["thisisadictionary"], preferences.Default["firstthing"].Values["thisisadictionary"]);
            Assert.IsType<object[]>(preferences.Default["firstthing"].Values["thisisanarray"]);
            Assert.Equal(new object[40], preferences.Default["firstthing"].Values["thisisanarray"]);
        }

        [Fact]
        public void TestCopyConstructor()
        {
            var preferences = new Preferences();
            var key1 = new Preferences.Key("org.raisingthefloor.test", "one");
            var key2 = new Preferences.Key("org.raisingthefloor.test", "two");
            var key3 = new Preferences.Key("org.raisingthefloor.test", "three");
            preferences.Set(key1, "Hello");
            preferences.Set(key2, 12L);
            preferences.Set(key3, new Dictionary<string, string>()
            {
                { "a", "value1" },
                { "b", "value2" }
            });

            var preferences2 = new Preferences(preferences);

            var value = preferences2.Get(key1);
            Assert.IsType<string>(value);
            Assert.Equal("Hello", (string)value);
            value = preferences2.Get(key2);
            Assert.IsType<long>(value);
            Assert.Equal(12, (long)value);
            value = preferences2.Get(key3);
            Assert.IsType<Dictionary<string, string>>(value);
            Assert.Equal("value1", ((Dictionary<string, string>)value)["a"]);
            Assert.Equal("value2", ((Dictionary<string, string>)value)["b"]);

            preferences.Set(key1, "World");
            value = preferences2.Get(key1);
            Assert.IsType<string>(value);
            Assert.Equal("Hello", (string)value);

            preferences2.Set(key1, "Testing");
            value = preferences.Get(key1);
            Assert.IsType<string>(value);
            Assert.Equal("World", (string)value);

            preferences.Set(key3, new Dictionary<string, string>()
            {
                { "a", "changed1" }
            });

            value = preferences2.Get(key3);
            Assert.IsType<Dictionary<string, string>>(value);
            Assert.Equal("value1", ((Dictionary<string, string>)value)["a"]);
            Assert.Equal("value2", ((Dictionary<string, string>)value)["b"]);

            preferences2.Set(key3, new Dictionary<string, string>()
            {
                { "a", "changed2" }
            });

            value = preferences.Get(key3);
            Assert.IsType<Dictionary<string, string>>(value);
            Assert.Equal("changed1", ((Dictionary<string, string>)value)["a"]);
        }

        [Fact]
        void TestRemove()
        {
            var preferences = new Preferences();
            var key1 = new Preferences.Key("org.raisingthefloor.test", "one");
            var key2 = new Preferences.Key("org.raisingthefloor.test", "two");
            var key3 = new Preferences.Key("org.raisingthefloor.test2", "three");
            preferences.Set(key1, "Hello");
            preferences.Set(key2, 12L);
            preferences.Set(key3, new Dictionary<string, string>()
            {
                { "a", "value1" },
                { "b", "value2" }
            });

            Assert.True(preferences.Default.TryGetValue("org.raisingthefloor.test", out var solutionPreferences));
            Assert.True(solutionPreferences.Values.ContainsKey("one"));
            Assert.True(solutionPreferences.Values.ContainsKey("two"));
            Assert.True(preferences.Default.TryGetValue("org.raisingthefloor.test2", out solutionPreferences));
            Assert.True(solutionPreferences.Values.ContainsKey("three"));

            preferences.Remove(key1);

            Assert.True(preferences.Default.TryGetValue("org.raisingthefloor.test", out solutionPreferences));
            Assert.False(solutionPreferences.Values.ContainsKey("one"));
            Assert.True(solutionPreferences.Values.ContainsKey("two"));
            Assert.True(preferences.Default.TryGetValue("org.raisingthefloor.test2", out solutionPreferences));
            Assert.True(solutionPreferences.Values.ContainsKey("three"));
            
            preferences.Remove(key3);

            Assert.True(preferences.Default.TryGetValue("org.raisingthefloor.test", out solutionPreferences));
            Assert.False(solutionPreferences.Values.ContainsKey("one"));
            Assert.True(solutionPreferences.Values.ContainsKey("two"));
            Assert.False(preferences.Default.TryGetValue("org.raisingthefloor.test2", out solutionPreferences));

            preferences.Remove(key2);

            Assert.False(preferences.Default.TryGetValue("org.raisingthefloor.test", out solutionPreferences));
            Assert.False(preferences.Default.TryGetValue("org.raisingthefloor.test2", out solutionPreferences));
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
                Default["firstthing"].Values.Add("thisisaninteger", 52L);
                Default["firstthing"].Values.Add("thisisaboolean", true);
                Dictionary<string, object> dict = new Dictionary<string, object>() { { "one", 1L }, { "two", 2L }, { "three", 3L } };
                Default["firstthing"].Values.Add("thisisadictionary", dict);
                object[] arr = new object[10];
                arr[5] = 413L;
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