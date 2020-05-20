using Microsoft.Extensions.Logging;
using Morphic.Settings.Spi;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

#nullable enable

namespace Morphic.Settings.Tests
{
    public class SystemParametersInfoSettingFinalizerTests
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
        private static int callCount = 0;
        private static pf passfail;

        private class MockSystemParametersInfo : ISystemParametersInfo
        {
            public bool Call(SystemParametersInfo.Action action, int parameter1, object? parameter2, bool updateUserProfile = false, bool sendChange = false)
            {
                if (passfail == pf.PASS) return true;
                else if (passfail == pf.FAIL) return false;
                else throw new ArgumentException();
            }
        }

        [Theory]
        [InlineData(SystemParametersInfo.Action.SetCursors, pf.PASS)]
        [InlineData(SystemParametersInfo.Action.SetCursors, pf.FAIL)]
        [InlineData(SystemParametersInfo.Action.SetCursors, pf.CRASH)]
        public async void TestRun(SystemParametersInfo.Action action, pf passfail)
        {
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<SystemParametersInfoSettingsFinalizer>();
            callCount = 0;
            SystemParametersInfoSettingFinalizerTests.passfail = passfail;

            var finalizer = new SystemParametersInfoSettingsFinalizer(new SystemParametersInfoSettingFinalizerDescription(action), new MockSystemParametersInfo(), logger);
            var result = await finalizer.Run();

            if (passfail == pf.PASS) Assert.True(result);
            else Assert.False(result);
        }
    }
}

#nullable disable