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
using Morphic.Settings.Spi;
using System;
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
                ++callCount;
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
            Assert.Equal(1, callCount);
        }
    }
}

#nullable disable