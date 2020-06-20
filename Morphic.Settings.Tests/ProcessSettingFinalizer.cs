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

    public class ProcessSettingFinalizerTests
    {

#nullable enable
        private class MockProcessManager : IProcessManager
        {
            public delegate bool Responder(string exe);

            public Responder NextIsRunningResponder = null!;
            public Responder NextStartResponder = null!;
            public Responder NextStopResponder = null!;

            public Task<bool> IsInstalled(string exe)
            {
                return Task.FromResult(true);
            }

            public Task<bool> IsRunning(string exe)
            {
                return Task.FromResult(NextIsRunningResponder.Invoke(exe));
            }

            public Task<bool> Start(string exe)
            {
                return Task.FromResult(NextStartResponder.Invoke(exe));
            }

            public Task<bool> Stop(string exe)
            {
                return Task.FromResult(NextStopResponder.Invoke(exe));
            }
        }

#nullable disable

        [Theory]
        [InlineData("test.exe", ProcessAction.Start, true, true, true, true, 0, 0)]
        [InlineData("test2.exe", ProcessAction.Start, false, true, true, true, 1, 0)]
        [InlineData("test.exe", ProcessAction.Stop, true, true, true, true, 0, 1)]
        [InlineData("test2.exe", ProcessAction.Stop, false, true, true, true, 0, 0)]
        [InlineData("test.exe", ProcessAction.Restart, true, true, true, true, 1, 1)]
        [InlineData("test2.exe", ProcessAction.Restart, false, true, true, true, 0, 0)]
        [InlineData("test.exe", ProcessAction.Start, true, false, false, true, 0, 0)]
        [InlineData("test2.exe", ProcessAction.Start, false, false, false, false, 1, 0)]
        [InlineData("test.exe", ProcessAction.Stop, true, false, false, false, 0, 1)]
        [InlineData("test2.exe", ProcessAction.Stop, false, false, false, true, 0, 0)]
        [InlineData("test.exe", ProcessAction.Restart, true, false, false, false, 0, 1)]
        [InlineData("test2.exe", ProcessAction.Restart, false, false, false, true, 0, 0)]
        public async Task TestRun(string exe, ProcessAction action, bool isRunning, bool startResult, bool stopResult, bool expectedSuccess, int expectedStartCount, int expectedStopCount)
        {
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<ProcessSettingFinalizer>();
            var processManager = new MockProcessManager();
            var description = new ProcessSettingFinalizerDescription(exe, action);
            var finalizer = new ProcessSettingFinalizer(description, processManager, logger);

            processManager.NextIsRunningResponder = (string exe_) =>
            {
                Assert.Equal(exe, exe_);
                return isRunning;
            };

            var startCount = 0;
            processManager.NextStartResponder = (string exe_) =>
            {
                Assert.Equal(exe, exe_);
                ++startCount;
                return startResult;
            };
            var stopCount = 0;
            processManager.NextStopResponder = (string exe_) =>
            {
                Assert.Equal(exe, exe_);
                ++stopCount;
                return stopResult;
            };
            var success = await finalizer.Run();
            Assert.Equal(expectedSuccess, success);
            Assert.Equal(expectedStartCount, startCount);
            Assert.Equal(expectedStopCount, stopCount);
        }


        [Theory]
        [InlineData("test.exe", ProcessAction.Start, typeof(Exception))]
        [InlineData("test.exe", ProcessAction.Start, typeof(ArgumentException))]
        [InlineData("test.exe", ProcessAction.Stop, typeof(Exception))]
        [InlineData("test.exe", ProcessAction.Stop, typeof(ArgumentException))]
        [InlineData("test.exe", ProcessAction.Restart, typeof(Exception))]
        [InlineData("test.exe", ProcessAction.Restart, typeof(ArgumentException))]
        public async Task TestRunException(string exe, ProcessAction action, Type exceptionType)
        {
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<ProcessSettingFinalizer>();
            var processManager = new MockProcessManager();
            var description = new ProcessSettingFinalizerDescription(exe, action);
            var finalizer = new ProcessSettingFinalizer(description, processManager, logger);

            var count = 0;
            processManager.NextIsRunningResponder = (string exe_) =>
            {
                Assert.Equal(exe, exe_);
                ++count;
                throw (Exception)Activator.CreateInstance(exceptionType);
            };
            var success = await finalizer.Run();
            Assert.False(success);
        }

    }
}