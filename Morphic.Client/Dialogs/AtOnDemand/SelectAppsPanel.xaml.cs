// Copyright 2020-2023 Raising the Floor - US, Inc.
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

namespace Morphic.Client.Dialogs.AtOnDemand;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elements;
using Morphic.Client.Bar.UI;
using Service;

public partial class SelectAppsPanel : StackPanel, IStepPanel
{
    private readonly MorphicSession morphicSession;
    private readonly IServiceProvider serviceProvider;

    public bool ApplyPreferencesAfterLogin { get; set; } = false;

    internal List<AtSoftwareDetails> ListOfAtSoftware { get; set; } = new();

    public SelectAppsPanel(MorphicSession morphicSession, IServiceProvider serviceProvider)
    {
        this.morphicSession = morphicSession;
        this.serviceProvider = serviceProvider;
        this.InitializeComponent();
    }

    private void Panel_Loaded(object sender, RoutedEventArgs e)
    {
        // display a checkbox for each available app
        foreach (var availableApp in this.ListOfAtSoftware)
        {
            var itemContent = availableApp.ProductName + " by " + availableApp.ManufacturerName;
            var itemCheckbox = new CheckBox() { Margin = new Thickness(0, 5, 0, 5), IsChecked = true, Content = itemContent, Tag = availableApp.ShortName };

            this.AvailableAppsStackPanel.Children.Add(itemCheckbox);
        }

        // update our window title to indicate that we're logged in and are now installing assistive apps (i.e. using AToD)
        var ourWindow = this.FindVisualParent<Window>();
        if (ourWindow is not null)
        {
            ourWindow!.Title = "Install Assistive Apps";
        }
    }

    private void SkipAllButton_Clicked(object sender, RoutedEventArgs e)
    {
        var atOnDemandCompletePanel = this.StepFrame.PushPanel<Morphic.Client.Dialogs.AtOnDemand.AtOnDemandCompletePanel>();
        atOnDemandCompletePanel.ApplyPreferencesAfterLogin = this.ApplyPreferencesAfterLogin;
        atOnDemandCompletePanel.ListOfInstalledApps = new(); // clear out the list of installed apps (since we're skipping them all)
        atOnDemandCompletePanel.Completed += (o, args) => this.Completed?.Invoke(this, EventArgs.Empty);
    }

    private void InstallButton_Clicked(object sender, RoutedEventArgs e)
    {
        List<AtSoftwareDetails> appsToInstall = new();
        foreach (var uiElement in this.AvailableAppsStackPanel.Children)
        {
            Debug.Assert(uiElement is CheckBox, "Child UI element of the 'available apps stack panel' is not a checkbox");
            var uiElementAsCheckbox = (CheckBox)uiElement;
            if (uiElementAsCheckbox.IsChecked == true)
            {
                var appShortName = (string)uiElementAsCheckbox.Tag;
                var appDetailsList = this.ListOfAtSoftware.Where(item => item.ShortName == appShortName).ToList();
                if (appDetailsList.Count != 1)
                {
                    Debug.Assert(false, "Multiple apps match the same shortname");
                }
                else
                {
                    var appDetails = appDetailsList[0];
                    appsToInstall.Add(appDetails);
                }
            }
        }

        var downloadAndInstallAppsPanel = this.StepFrame.PushPanel<Morphic.Client.Dialogs.AtOnDemand.DownloadAndInstallAppsPanel>();
        downloadAndInstallAppsPanel.ApplyPreferencesAfterLogin = this.ApplyPreferencesAfterLogin;
        downloadAndInstallAppsPanel.AppsToInstall = appsToInstall;
        downloadAndInstallAppsPanel.Completed += (o, args) => this.Completed?.Invoke(this, EventArgs.Empty);
    }

    #region IStepPanel

    public StepFrame StepFrame { get; set; } = null!;
    public event EventHandler? Completed;
    #endregion

}

