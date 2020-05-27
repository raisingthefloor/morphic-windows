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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Morphic.Settings.SystemSettings;
using System;
using System.Collections.Generic;
using System.Security;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Morphic.Settings.Tests
{
    public class SystemSettingHandlerTests
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
        private static int createCount = 0;
        private static int getCount = 0;
        private static int setCount = 0;

        private class MockSystemSetting : ISystemSetting
        {
            public delegate object? GetResponder();
            public delegate void SetResponder(object value);

            public static GetResponder nextGetResponder = () =>
            {
                throw new NotImplementedException();
            };

            public static SetResponder nextSetResponder = (object value) =>
            {
                throw new NotImplementedException();
            };
            public async Task<object?> GetValue()
            {
                ++getCount;
                await Task.Delay(0);
                return nextGetResponder();
            }

            public async Task SetValue(object value)
            {
                ++setCount;
                await Task.Delay(0);
                nextSetResponder(value);
            }
        }

        private class MockSystemSettingFactory : ISystemSettingFactory
        {
            public ISystemSetting Create(string id, IServiceProvider serviceProvider)
            {
                ++createCount;
                return new MockSystemSetting();
            }
        }

        [Theory]
        [InlineData(Setting.ValueKind.String, SystemValueKind.String, "Hello", "Hello")]
        [InlineData(Setting.ValueKind.Integer, SystemValueKind.Integer, 52U, 52L)]
        [InlineData(Setting.ValueKind.Boolean, SystemValueKind.Boolean, true, true)]
        [InlineData(Setting.ValueKind.Boolean, SystemValueKind.Boolean, false, false)]
        [InlineData(Setting.ValueKind.Integer, SystemValueKind.IdPrefixedEnum, "thesetting3", 3L)]
        [InlineData(Setting.ValueKind.Integer, SystemValueKind.Integer, "zero", 0L)]
        [InlineData(Setting.ValueKind.Integer, SystemValueKind.Integer, "one", 1L)]
        [InlineData(Setting.ValueKind.Integer, SystemValueKind.Integer, "two", 2L)]
        public async Task TestCapturePass(Setting.ValueKind kind, SystemValueKind valueKind, object systemValue, object value)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<ISystemSettingFactory, MockSystemSettingFactory>();
            var mockFactory = new MockSystemSettingFactory();
            var serviceProvider = services.BuildServiceProvider();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<SystemSettingHandler>();
            createCount = 0;
            getCount = 0;
            setCount = 0;
            var handlerDescription = new SystemSettingHandlerDescription("thesetting", valueKind);
            handlerDescription.IntegerMap = new string[3] { "zero", "one", "two" };
            handlerDescription.ReverseIntegerMap = new Dictionary<string, long> { { "zero", 0L }, { "one", 1L }, { "two", 2L } };
            var setting = new Setting()
            {
                Name = "test",
                Kind = kind,
                HandlerDescription = handlerDescription
            };
            var handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextGetResponder = () =>
            {
                return systemValue;
            };

            var result = await handler.Capture();
            Assert.Equal(1, createCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.True(result.Success);
            Assert.NotNull(result.Value);
            Assert.Equal(value, result.Value);
        }

        [Theory]
        [InlineData(Setting.ValueKind.Integer, SystemValueKind.String, "Hello")]
        [InlineData(Setting.ValueKind.Boolean, SystemValueKind.String, "Hello")]
        [InlineData(Setting.ValueKind.Double, SystemValueKind.String, "Hello")]
        [InlineData(Setting.ValueKind.String, SystemValueKind.Integer, 52L)]
        [InlineData(Setting.ValueKind.Double, SystemValueKind.Integer, 52L)]
        [InlineData(Setting.ValueKind.Boolean, SystemValueKind.Integer, 52L)]
        [InlineData(Setting.ValueKind.Integer, SystemValueKind.Boolean, true)]
        [InlineData(Setting.ValueKind.String, SystemValueKind.Boolean, true)]
        [InlineData(Setting.ValueKind.Double, SystemValueKind.Boolean, true)]
        [InlineData(Setting.ValueKind.Boolean, SystemValueKind.IdPrefixedEnum, "thesetting3")]
        [InlineData(Setting.ValueKind.Double, SystemValueKind.IdPrefixedEnum, "thesetting3")]
        public async Task TestCaptureFail(Setting.ValueKind kind, SystemValueKind valueKind, object value)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<ISystemSettingFactory, MockSystemSettingFactory>();
            var mockFactory = new MockSystemSettingFactory();
            var serviceProvider = services.BuildServiceProvider();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<SystemSettingHandler>();
            createCount = 0;
            getCount = 0;
            setCount = 0;

            var setting = new Setting()
            {
                Name = "test",
                Kind = kind,
                HandlerDescription = new SystemSettingHandlerDescription("thesetting", valueKind)
            };
            var handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextGetResponder = () =>
            {
                return value;
            };

            var result = await handler.Capture();
            Assert.Equal(1, createCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.False(result.Success);
            Assert.Null(result.Value);
        }

        [Fact]
        public async Task TestCaptureExceptions()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<ISystemSettingFactory, MockSystemSettingFactory>();
            var mockFactory = new MockSystemSettingFactory();
            var serviceProvider = services.BuildServiceProvider();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<SystemSettingHandler>();

            var setting = new Setting()
            {
                Name = "test",
                HandlerDescription = new SystemSettingHandlerDescription("thesetting", SystemValueKind.Boolean)
            };

            createCount = 0;
            getCount = 0;
            setCount = 0;
            var handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextGetResponder = () =>
            {
                throw new ArgumentException();
            };
            var result = await handler.Capture();
            Assert.Equal(1, createCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.False(result.Success);
            Assert.Null(result.Value);
            createCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextGetResponder = () =>
            {
                throw new ArgumentNullException();
            };
            result = await handler.Capture();
            Assert.Equal(1, createCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.False(result.Success);
            Assert.Null(result.Value);
            createCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextGetResponder = () =>
            {
                throw new SecurityException();
            };
            result = await handler.Capture();
            Assert.Equal(1, createCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.False(result.Success);
            Assert.Null(result.Value);
            createCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextGetResponder = () =>
            {
                throw new ObjectDisposedException("ayylmao");
            };
            result = await handler.Capture();
            Assert.Equal(1, createCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.False(result.Success);
            Assert.Null(result.Value);
            createCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextGetResponder = () =>
            {
                throw new System.IO.IOException();
            };
            result = await handler.Capture();
            Assert.Equal(1, createCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.False(result.Success);
            Assert.Null(result.Value);
            createCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextGetResponder = () =>
            {
                throw new UnauthorizedAccessException();
            };
            result = await handler.Capture();
            Assert.Equal(1, createCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.False(result.Success);
            Assert.Null(result.Value);
        }

        [Theory]
        [InlineData(SystemValueKind.String, "Hello", "Hello")]
        [InlineData(SystemValueKind.String, 0L, "zero")]
        [InlineData(SystemValueKind.String, 1L, "one")]
        [InlineData(SystemValueKind.String, 2L, "two")]
        [InlineData(SystemValueKind.Integer, 52L, 52U)]
        [InlineData(SystemValueKind.Boolean, true, true)]
        [InlineData(SystemValueKind.Boolean, false, false)]
        [InlineData(SystemValueKind.IdPrefixedEnum, 3L, "thesetting3")]
        public async Task TestApplyPass(SystemValueKind valueKind, object value, object sysValue)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<ISystemSettingFactory, MockSystemSettingFactory>();
            var mockFactory = new MockSystemSettingFactory();
            var serviceProvider = services.BuildServiceProvider();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<SystemSettingHandler>();
            createCount = 0;
            getCount = 0;
            setCount = 0;
            var handlerDescription = new SystemSettingHandlerDescription("thesetting", valueKind);
            handlerDescription.IntegerMap = new string[3] { "zero", "one", "two" };
            var setting = new Setting()
            {
                Name = "test",
                HandlerDescription = handlerDescription
            };
            var handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextSetResponder = (object nvalue) =>
            {
                Assert.Equal(sysValue, nvalue);
            };

            var pass = await handler.Apply(value);
            Assert.Equal(1, createCount);
            Assert.Equal(0, getCount);
            Assert.Equal(1, setCount);
            Assert.True(pass);
        }

        [Theory]
        [InlineData(SystemValueKind.String, 52L)]
        [InlineData(SystemValueKind.String, 3.14159d)]
        [InlineData(SystemValueKind.String, true)]
        [InlineData(SystemValueKind.Integer, true)]
        [InlineData(SystemValueKind.Integer, "12345")]
        [InlineData(SystemValueKind.Integer, 3.14159d)]
        [InlineData(SystemValueKind.Boolean, 1L)]
        [InlineData(SystemValueKind.Boolean, "true")]
        [InlineData(SystemValueKind.Boolean, 1.0d)]
        [InlineData(SystemValueKind.IdPrefixedEnum, "3")]
        [InlineData(SystemValueKind.IdPrefixedEnum, true)]
        [InlineData(SystemValueKind.IdPrefixedEnum, 3.0d)]
        public async Task TestApplyFail(SystemValueKind valueKind, object value)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<ISystemSettingFactory, MockSystemSettingFactory>();
            var mockFactory = new MockSystemSettingFactory();
            var serviceProvider = services.BuildServiceProvider();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<SystemSettingHandler>();
            createCount = 0;
            getCount = 0;
            setCount = 0;

            var setting = new Setting()
            {
                Name = "test",
                HandlerDescription = new SystemSettingHandlerDescription("thesetting", valueKind)
            };
            var handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextSetResponder = (object nvalue) =>
            {
                Assert.Equal(value, nvalue);
            };

            var pass = await handler.Apply(value);
            Assert.Equal(1, createCount);
            Assert.Equal(0, getCount);
            Assert.Equal(0, setCount);
            Assert.False(pass);
        }

        [Fact]
        public async Task TestApplyExceptions()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<ISystemSettingFactory, MockSystemSettingFactory>();
            var mockFactory = new MockSystemSettingFactory();
            var serviceProvider = services.BuildServiceProvider();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<SystemSettingHandler>();

            var setting = new Setting()
            {
                Name = "test",
                Kind = Setting.ValueKind.Boolean,
                HandlerDescription = new SystemSettingHandlerDescription("thesetting", SystemValueKind.Boolean)
            };

            createCount = 0;
            getCount = 0;
            setCount = 0;
            var handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextSetResponder = (object nvalue) =>
            {
                throw new ArgumentException();
            };
            var pass = await handler.Apply(true);
            Assert.Equal(1, createCount);
            Assert.Equal(0, getCount);
            Assert.Equal(1, setCount);
            Assert.False(pass);
            createCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextSetResponder = (object nvalue) =>
            {
                throw new ArgumentNullException();
            };
            pass = await handler.Apply(true);
            Assert.Equal(1, createCount);
            Assert.Equal(0, getCount);
            Assert.Equal(1, setCount);
            Assert.False(pass);
            createCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextSetResponder = (object nvalue) =>
            {
                throw new SecurityException();
            };
            pass = await handler.Apply(true);
            Assert.Equal(1, createCount);
            Assert.Equal(0, getCount);
            Assert.Equal(1, setCount);
            Assert.False(pass);
            createCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextSetResponder = (object nvalue) =>
            {
                throw new ObjectDisposedException("ayylmao");
            };
            pass = await handler.Apply(true);
            Assert.Equal(1, createCount);
            Assert.Equal(0, getCount);
            Assert.Equal(1, setCount);
            Assert.False(pass);
            createCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextSetResponder = (object nvalue) =>
            {
                throw new System.IO.IOException();
            };
            pass = await handler.Apply(true);
            Assert.Equal(1, createCount);
            Assert.Equal(0, getCount);
            Assert.Equal(1, setCount);
            Assert.False(pass);
            createCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new SystemSettingHandler(setting, mockFactory, serviceProvider, logger);
            MockSystemSetting.nextSetResponder = (object nvalue) =>
            {
                throw new UnauthorizedAccessException();
            };
            pass = await handler.Apply(true);
            Assert.Equal(1, createCount);
            Assert.Equal(0, getCount);
            Assert.Equal(1, setCount);
            Assert.False(pass);
        }
    }
}

#nullable disable