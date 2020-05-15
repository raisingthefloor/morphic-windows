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
using Xunit;
using System.Collections.Generic;
using System.Text.Json;

namespace Morphic.Core.Tests
{
    public class JsonExtensionTests
    {
        [Fact]
        public void TestBooleans()
        {
            var json = @"{""a"": true, ""b"": false}";
            var options = new JsonSerializerOptions();
            var converter = new JsonElementInferredTypeConverter();
            options.Converters.Add(converter);
            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);
            object o;
            Assert.True(result.TryGetValue("a", out o));
            Assert.IsType<bool>(o);
            Assert.True((bool)o);
            Assert.True(result.TryGetValue("b", out o));
            Assert.IsType<bool>(o);
            Assert.False((bool)o);
        }

        [Fact]
        public void TestIntegers()
        {
            var json = @"{""a"": 12, ""b"": 0}";
            var options = new JsonSerializerOptions();
            var converter = new JsonElementInferredTypeConverter();
            options.Converters.Add(converter);
            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);
            object o;
            Assert.True(result.TryGetValue("a", out o));
            Assert.IsType<long>(o);
            Assert.Equal(12, (long)o);
            Assert.True(result.TryGetValue("b", out o));
            Assert.IsType<long>(o);
            Assert.Equal(0, (long)o);
        }

        [Fact]
        public void TestFloats()
        {
            var json = @"{""a"": 12.5, ""b"": 0.1}";
            var options = new JsonSerializerOptions();
            var converter = new JsonElementInferredTypeConverter();
            options.Converters.Add(converter);
            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);
            object o;
            Assert.True(result.TryGetValue("a", out o));
            Assert.IsType<double>(o);
            Assert.True(Math.Abs(12.5 - (double)o) < 0.001);
            Assert.True(result.TryGetValue("b", out o));
            Assert.IsType<double>(o);
            Assert.True(Math.Abs(0.1 - (double)o) < 0.001);
        }

        [Fact]
        public void TestStrings()
        {
            var json = @"{""a"": ""hello"", ""b"": """"}";
            var options = new JsonSerializerOptions();
            var converter = new JsonElementInferredTypeConverter();
            options.Converters.Add(converter);
            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);
            object o;
            Assert.True(result.TryGetValue("a", out o));
            Assert.IsType<string>(o);
            Assert.Equal("hello", (string)o);
            Assert.True(result.TryGetValue("b", out o));
            Assert.IsType<string>(o);
            Assert.Equal("", (string)o);
        }

        [Fact]
        public void TestNull()
        {
            var json = @"{""a"": null}";
            var options = new JsonSerializerOptions();
            var converter = new JsonElementInferredTypeConverter();
            options.Converters.Add(converter);
            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);
            object o;
            Assert.True(result.TryGetValue("a", out o));
            Assert.Null(o);
        }

        [Fact]
        public void TestArray()
        {
            var json = @"{""a"": [1, ""two"", null, 4.1, true]}";
            var options = new JsonSerializerOptions();
            var converter = new JsonElementInferredTypeConverter();
            options.Converters.Add(converter);
            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);
            object o;
            Assert.True(result.TryGetValue("a", out o));
            Assert.IsType<object[]>(o);
            var array = (object[])o;
            Assert.Equal(5, array.Length);

            Assert.IsType<long>(array[0]);
            Assert.Equal(1, (long)array[0]);
            Assert.IsType<string>(array[1]);
            Assert.Equal("two", (string)array[1]);
            Assert.Null(array[2]);
            Assert.IsType<double>(array[3]);
            Assert.True(Math.Abs(4.1 - (double)array[3]) < 0.001);
            Assert.IsType<bool>(array[4]);
            Assert.True((bool)array[4]);
        }

        [Fact]
        public void TestObject()
        {
            var json = @"{""a"": {""first"": 1, ""second"": ""two""}}";
            var options = new JsonSerializerOptions();
            var converter = new JsonElementInferredTypeConverter();
            options.Converters.Add(converter);
            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);
            object o;
            Assert.True(result.TryGetValue("a", out o));
            Assert.IsType<Dictionary<string, object>>(o);
            var dictionary = (Dictionary<string, object>)o;
            Assert.True(dictionary.TryGetValue("first", out o));
            Assert.IsType<long>(o);
            Assert.Equal(1, (long)o);
            Assert.True(dictionary.TryGetValue("second", out o));
            Assert.IsType<string>(o);
            Assert.Equal("two", (string)o);
        }
    }
}
