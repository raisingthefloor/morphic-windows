// Copyright 2020-2022 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/master/LICENSE.txt
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

namespace Morphic;

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    bool _windowsAppSdkWasManuallyBootstrapped = false;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        bool isRunningAsPackagedApp;

        var isRunningAsPackagedAppResult = Morphic.WindowsNative.Packaging.Package.IsRunningAsPackagedApp();
        if (isRunningAsPackagedAppResult.IsError == true)
        {
            isRunningAsPackagedApp = false;
        }
        else
        {
            isRunningAsPackagedApp = isRunningAsPackagedAppResult.Value!;
        }

        if (isRunningAsPackagedApp == false)
        {
            // initialize the Windows App SDK using the appropriate bootstrapper
            // NOTE: the majorMinor version specified here is v1.1 (major 0x0001, minor 0x0001)
            Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Initialize(0x0001_0001);
            _windowsAppSdkWasManuallyBootstrapped = true;
        }
		
        // TEMP (test): create a Windows App SDK Resource Manager using the resource index generated during build.
        var resourceManager = new Microsoft.Windows.ApplicationModel.Resources.ResourceManager();

        // TEMP (test): look up a string from our Resources.resw file using the file's name
//        var welcomePlaceholderMessage = resourceManager.MainResourceMap.GetValue("Resources/WelcomeMessage");
//        MessageBox.Show(welcomePlaceholderMessage.ValueAsString);
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        if (_windowsAppSdkWasManuallyBootstrapped == true)
        {
            // release the Dynamic Dependency Lifetime Manager (DDLM) and clean up the Windows App SDK
            Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Shutdown();
        }
    }
}
