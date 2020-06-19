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
using Morphic.Settings.Process;
using NuGet.Frameworks;
using System;
using System.Security;
using System.Threading.Tasks;
using Xunit;


namespace Morphic.Settings.Tests
{

    public class ProcessSettingHandlerTests
    {

#nullable enable
        private class MockProcessManager : IProcessManager
        {
            public delegate bool Responder(string appPathKey);

            public Responder NextIsRunningResponder = null!;
            public Responder NextStartResponder = null!;
            public Responder NextStopResponder = null!;

            public Task<bool> IsInstalled(string appPathKey)
            {
                return Task.FromResult(true);
            }

            public Task<bool> IsRunning(string appPathKey)
            {
                return Task.FromResult(NextIsRunningResponder.Invoke(appPathKey));
            }

            public Task<bool> Start(string appPathKey)
            {
                return Task.FromResult(NextStartResponder.Invoke(appPathKey));
            }

            public Task<bool> Stop(string appPathKey)
            {
                return Task.FromResult(NextStopResponder.Invoke(appPathKey));
            }
        }

#nullable disable

        [Theory]
        [InlineData("test", "test.exe", true, true)]
        [InlineData("test2", "test2.exe", true, false)]
        public async Task TestCaptureRunning(string name, string appKeyPath, bool success, bool resultValue)
        {
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<ProcessSettingHandler>();
            var processManager = new MockProcessManager();
            var setting = new Setting()
            {
                Name = name,
                Kind = Setting.ValueKind.Boolean,
                Default = false,
                HandlerDescription = new ProcessSettingHandlerDescription(appKeyPath, ProcessState.Running)
            };
            var handler = new ProcessSettingHandler(setting, processManager, logger);

            var count = 0;
            processManager.NextIsRunningResponder = (string appKeyPath_) =>
            {
                Assert.Equal(appKeyPath, appKeyPath_);
                ++count;
                return resultValue;
            };
            var result = await handler.Capture();
            Assert.Equal(1, count);
            Assert.Equal(success, result.Success);
            Assert.IsType<bool>(result.Value);
            Assert.Equal(resultValue, (bool)result.Value);
        }

        [Theory]
        [InlineData("test", "test.exe", typeof(Exception))]
        [InlineData("test", "test.exe", typeof(ArgumentException))]
        public async Task TestCaptureRunningException(string name, string appKeyPath, Type exceptionType)
        {
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<ProcessSettingHandler>();
            var processManager = new MockProcessManager();
            var setting = new Setting()
            {
                Name = name,
                Kind = Setting.ValueKind.Boolean,
                Default = false,
                HandlerDescription = new ProcessSettingHandlerDescription(appKeyPath, ProcessState.Running)
            };
            var handler = new ProcessSettingHandler(setting, processManager, logger);

            var count = 0;
            processManager.NextIsRunningResponder = (string appKeyPath_) =>
            {
                Assert.Equal(appKeyPath, appKeyPath_);
                ++count;
                throw (Exception)Activator.CreateInstance(exceptionType);
            };
            var result = await handler.Capture();
            Assert.Equal(1, count);
            Assert.False(result.Success);
        }

        [Theory]
        [InlineData("test", "test.exe", true, true, 0, 0)]
        [InlineData("test", "test.exe", true, false, 1, 0)]
        [InlineData("test2", "test2.exe", false, true, 0, 1)]
        [InlineData("test2", "test2.exe", false, false, 0, 0)]
        public async Task TestApplyRunning(string name, string appKeyPath, bool value, bool isRunning, int expectedStartCount, int expectedStopCount)
        {
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<ProcessSettingHandler>();
            var processManager = new MockProcessManager();
            var setting = new Setting()
            {
                Name = name,
                Kind = Setting.ValueKind.Boolean,
                Default = false,
                HandlerDescription = new ProcessSettingHandlerDescription(appKeyPath, ProcessState.Running)
            };
            var handler = new ProcessSettingHandler(setting, processManager, logger);

            processManager.NextIsRunningResponder = (string appKeyPath_) =>
            {
                Assert.Equal(appKeyPath, appKeyPath_);
                return isRunning;
            };

            var startCount = 0;
            processManager.NextStartResponder = (string appKeyPath_) =>
            {
                Assert.Equal(appKeyPath, appKeyPath_);
                ++startCount;
                return true;
            };
            var stopCount = 0;
            processManager.NextStopResponder = (string appKeyPath_) =>
            {
                Assert.Equal(appKeyPath, appKeyPath_);
                ++stopCount;
                return true;
            };
            var success = await handler.Apply(value);
            Assert.True(success);
            Assert.Equal(expectedStartCount, startCount);
            Assert.Equal(expectedStopCount, stopCount);
        }

        [Theory]
        [InlineData("test", "test.exe", true, true, 0, 0)]
        [InlineData("test", "test.exe", true, false, 1, 0)]
        [InlineData("test2", "test2.exe", false, true, 0, 1)]
        [InlineData("test2", "test2.exe", false, false, 0, 0)]
        public async Task TestApplyRunningFail(string name, string appKeyPath, bool value, bool isRunning, int expectedStartCount, int expectedStopCount)
        {
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<ProcessSettingHandler>();
            var processManager = new MockProcessManager();
            var setting = new Setting()
            {
                Name = name,
                Kind = Setting.ValueKind.Boolean,
                Default = false,
                HandlerDescription = new ProcessSettingHandlerDescription(appKeyPath, ProcessState.Running)
            };
            var handler = new ProcessSettingHandler(setting, processManager, logger);

            processManager.NextIsRunningResponder = (string appKeyPath_) =>
            {
                Assert.Equal(appKeyPath, appKeyPath_);
                return isRunning;
            };

            var startCount = 0;
            processManager.NextStartResponder = (string appKeyPath_) =>
            {
                Assert.Equal(appKeyPath, appKeyPath_);
                ++startCount;
                return false;
            };
            var stopCount = 0;
            processManager.NextStopResponder = (string appKeyPath_) =>
            {
                Assert.Equal(appKeyPath, appKeyPath_);
                ++stopCount;
                return false;
            };
            var success = await handler.Apply(value);
            Assert.Equal(expectedStartCount + expectedStopCount == 0, success);
            Assert.Equal(expectedStartCount, startCount);
            Assert.Equal(expectedStopCount, stopCount);
        }

        [Theory]
        [InlineData("test", "test.exe", 1, true)]
        [InlineData("test", "test.exe", 1L, true)]
        [InlineData("test", "test.exe", null, false)]
        [InlineData("test2", "test2.exe", "test", true)]
        [InlineData("test2", "test2.exe", 1.3, false)]
        [InlineData("test2", "test2.exe", 1.3f, false)]
        public async Task TestApplyRunningInvalidType(string name, string appKeyPath, object value, bool isRunning)
        {
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<ProcessSettingHandler>();
            var processManager = new MockProcessManager();
            var setting = new Setting()
            {
                Name = name,
                Kind = Setting.ValueKind.Boolean,
                Default = false,
                HandlerDescription = new ProcessSettingHandlerDescription(appKeyPath, ProcessState.Running)
            };
            var handler = new ProcessSettingHandler(setting, processManager, logger);

            processManager.NextIsRunningResponder = (string appKeyPath_) =>
            {
                Assert.Equal(appKeyPath, appKeyPath_);
                return isRunning;
            };

            var startCount = 0;
            processManager.NextStartResponder = (string appKeyPath_) =>
            {
                Assert.Equal(appKeyPath, appKeyPath_);
                ++startCount;
                return false;
            };
            var stopCount = 0;
            processManager.NextStopResponder = (string appKeyPath_) =>
            {
                Assert.Equal(appKeyPath, appKeyPath_);
                ++stopCount;
                return false;
            };
            var success = await handler.Apply(value);
            Assert.False(success);
            Assert.Equal(0, startCount);
            Assert.Equal(0, stopCount);
        }

        [Theory]
        [InlineData("test", "test.exe", true, typeof(Exception))]
        [InlineData("test", "test.exe", false, typeof(ArgumentException))]
        public async Task TestApplyRunningException(string name, string appKeyPath, bool value, Type exceptionType)
        {
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<ProcessSettingHandler>();
            var processManager = new MockProcessManager();
            var setting = new Setting()
            {
                Name = name,
                Kind = Setting.ValueKind.Boolean,
                Default = false,
                HandlerDescription = new ProcessSettingHandlerDescription(appKeyPath, ProcessState.Running)
            };
            var handler = new ProcessSettingHandler(setting, processManager, logger);

            var count = 0;
            processManager.NextIsRunningResponder = (string appKeyPath_) =>
            {
                Assert.Equal(appKeyPath, appKeyPath_);
                ++count;
                throw (Exception)Activator.CreateInstance(exceptionType);
            };
            var success = await handler.Apply(value);
            Assert.False(success);
        }

    }
}