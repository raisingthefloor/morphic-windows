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
using Morphic.Settings.Ini;
using System;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Morphic.Settings.Tests
{

    public class IniSettingHandlerTests
    {
        //PASS = everything worked normally
        //FAIL = it didn't work (get returns null) but it didn't throw an exception
        //CRASH = it threw an exception
        public enum pf
        {
            PASS,
            FAIL,
            CRASH
        }
        private static int openCount = 0;
        private static int getCount = 0;
        private static int setCount = 0;
        private static string? iniValue = null;
        private static pf passfail;

        private class MockIniFile : IIniFile
        {
            public string? GetValue(string section, string key)
            {
                ++getCount;
                if (passfail == pf.PASS) return iniValue;
                else if (passfail == pf.FAIL) return null;
                else throw new ArgumentException();
            }

            public void SetValue(string section, string key, string value)
            {
                ++setCount;
                if (passfail == pf.CRASH) throw new ArgumentException();
            }
        }

        private class MockIniFactory : IIniFileFactory
        {
            public IIniFile Open(string path)
            {
                ++openCount;
                return new MockIniFile();
            }
        }

        [Theory]
        [InlineData(Setting.ValueKind.String, "Hello", "Hello", pf.PASS)]
        [InlineData(Setting.ValueKind.String, "Hello", "Hello", pf.FAIL)]
        [InlineData(Setting.ValueKind.String, "Hello", "Hello", pf.CRASH)]
        [InlineData(Setting.ValueKind.Integer, "52", 52L, pf.PASS)]
        [InlineData(Setting.ValueKind.Integer, "52", 52L, pf.FAIL)]
        [InlineData(Setting.ValueKind.Integer, "52", 52L, pf.CRASH)]
        [InlineData(Setting.ValueKind.Double, "3.14159", 3.14159d, pf.PASS)]
        [InlineData(Setting.ValueKind.Double, "3.14159", 3.14159d, pf.FAIL)]
        [InlineData(Setting.ValueKind.Double, "3.14159", 3.14159d, pf.CRASH)]
        [InlineData(Setting.ValueKind.Boolean, "true", true, pf.PASS)]
        [InlineData(Setting.ValueKind.Boolean, "true", true, pf.FAIL)]
        [InlineData(Setting.ValueKind.Boolean, "true", true, pf.CRASH)]
        public async Task TestCapture(Setting.ValueKind kind, string storedvalue, object value, pf passfail)
        {
            var mockFactory = new MockIniFactory();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<IniSettingHandler>();
            openCount = 0;
            getCount = 0;
            setCount = 0;
            iniValue = storedvalue;
            IniSettingHandlerTests.passfail = passfail;

            var setting = new Setting()
            {
                Name = "test",
                Kind = kind,
                HandlerDescription = new IniSettingHandlerDescription("thefile", "thesection", "thekey")
            };
            var handler = new IniSettingHandler(setting, mockFactory, logger);

            var result = await handler.Capture();
            Assert.Equal(1, openCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            if (passfail == pf.PASS)
            {
                Assert.True(result.Success);
                Assert.NotNull(result.Value);
                Assert.Equal(value, result.Value);
            }
            else if(passfail == pf.FAIL)
            {
                Assert.True(result.Success);
                Assert.Null(result.Value);
            }
            else
            {
                Assert.False(result.Success);
                Assert.Null(result.Value);
            }
        }

        [Theory]
        [InlineData(Setting.ValueKind.String, "Hello", pf.PASS, true)]
        [InlineData(Setting.ValueKind.String, "Hello", pf.FAIL, true)]
        [InlineData(Setting.ValueKind.String, "Hello", pf.CRASH, false)]
        [InlineData(Setting.ValueKind.Integer, 52L, pf.PASS, true)]
        [InlineData(Setting.ValueKind.Integer, 52L, pf.FAIL, true)]
        [InlineData(Setting.ValueKind.Integer, 52L, pf.CRASH, false)]
        [InlineData(Setting.ValueKind.Boolean, true, pf.PASS, true)]
        [InlineData(Setting.ValueKind.Boolean, true, pf.FAIL, true)]
        [InlineData(Setting.ValueKind.Boolean, true, pf.CRASH, false)]
        [InlineData(Setting.ValueKind.Double, 3.14159d, pf.PASS, true)]
        [InlineData(Setting.ValueKind.Double, 3.14159d, pf.FAIL, true)]
        [InlineData(Setting.ValueKind.Double, 3.14159d, pf.CRASH, false)]
        public async Task TestApply(Setting.ValueKind kind, object value, pf passfail, bool success)
        {
            var mockFactory = new MockIniFactory();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<IniSettingHandler>();
            openCount = 0;
            getCount = 0;
            setCount = 0;
            IniSettingHandlerTests.passfail = passfail;

            var setting = new Setting()
            {
                Name = "test",
                Kind = kind,
                HandlerDescription = new IniSettingHandlerDescription("thefile", "thesection", "thekey")
            };
            var handler = new IniSettingHandler(setting, mockFactory, logger);

            var pass = await handler.Apply(value);
            if (success)
            {
                Assert.Equal(1, openCount);
                Assert.Equal(0, getCount);
                Assert.Equal(1, setCount);
                Assert.True(pass);
            }
            else
            {
                Assert.False(pass);
            }
        }
    }
}

#nullable disable