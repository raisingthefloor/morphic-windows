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
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elements;
using Service;
using Windows.Management.Deployment.Preview;

public partial class AtOnDemandCompletePanel : StackPanel, IStepPanel
{
    private readonly MorphicSession morphicSession;
    private readonly IServiceProvider serviceProvider;

    public bool ApplyPreferencesAfterLogin { get; set; } = false;

    internal List<AtSoftwareDetails> ListOfInstalledApps { get; set; } = new();

    public AtOnDemandCompletePanel(MorphicSession morphicSession, IServiceProvider serviceProvider)
    {
        this.morphicSession = morphicSession;
        this.serviceProvider = serviceProvider;
        this.InitializeComponent();
    }

    private void Panel_Loaded(object sender, RoutedEventArgs e)
    {
        // display the # of apps installed, along with a list of the apps
        this.NumberOfAppsInstalledTextBlockRun.Text = this.ListOfInstalledApps.Count.ToString();

        foreach (var installedApp in this.ListOfInstalledApps)
        {
            var itemStackPanel = new StackPanel() { Orientation = Orientation.Horizontal };
            //
            var darkGreenColor = System.Drawing.Color.DarkGreen;
            var bulletTextBlock = new TextBlock() { Margin = new Thickness(5, 0, 0, 0), Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(darkGreenColor.A, darkGreenColor.R, darkGreenColor.G, darkGreenColor.B)), Text = "•" };
            itemStackPanel.Children.Add(bulletTextBlock);
            //
            var appNameText = installedApp.ProductName + " by " + installedApp.ManufacturerName;
            var appNameTextBlock = new TextBlock() { Margin = new Thickness(5, 0, 0, 0), Text = appNameText };
            itemStackPanel.Children.Add(appNameTextBlock);

            this.InstalledAppsStackPanel.Children.Add(itemStackPanel);
        }
    }

    private void Close()
    {
        this.StepFrame.CloseWindow();
    }

    protected virtual void OnComplete()
    {
        this.Completed?.Invoke(this, EventArgs.Empty);
    }


    private void DoneButton_Clicked(object sender, RoutedEventArgs e)
    {
        // NOTE: we also do this in the LoginPanel if AToD was not necessary; we should consolidate this code to only do it in one place (which gets called after AToD has completed)
        // NOTE: this distinction between close and complete was done to mirror the Morphic v1.0 implementation in LoginPanel; it may be unnecessary and should be revisited with as part of login/capture/apply/atod UI flow updates
        if (this.ApplyPreferencesAfterLogin)
        {
            this.Close();
        }
        else
        {
            this.OnComplete();
        }
    }

    #region IStepPanel

    public StepFrame StepFrame { get; set; } = null!;
    public event EventHandler? Completed;
    #endregion

}

