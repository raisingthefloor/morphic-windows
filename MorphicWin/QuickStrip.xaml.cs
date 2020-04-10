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
using System.Windows;
using System.Windows.Controls;
using MorphicService;
using MorphicSettings;

namespace MorphicWin
{
    /// <summary>
    /// Interaction logic for QuickStrip.xaml
    /// </summary>
    public partial class QuickStrip : Window
    {
        public QuickStrip(Session session)
        {
            this.session = session;
            InitializeComponent();
            Deactivated += OnDeactivated;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            var currentValue = session.GetString("com.microsoft.windows.display", "zoom") ?? "normal";
            var currentLevel = Enum.Parse<Display.ZoomLevel>(currentValue, ignoreCase: true);
            foreach (var level in displayZoomLevels)
            {
                var item = new ComboBoxItem
                {
                    Content = level.Item2
                };
                displayZoomComboBox.Items.Add(item);
                if (level.Item1 == currentLevel)
                {
                    displayZoomComboBox.SelectedItem = item;
                }
            }
            displayZoomComboBox.SelectionChanged += DisplayZoomChanged;
        }

        private readonly Session session;

        private void OnDeactivated(object? sender, EventArgs e)
        {
            //Close();
        }

        private void OpenConfigurator(object? sender, RoutedEventArgs e)
        {
            App.Shared.OpenConfigurator();
        }

        private readonly (Display.ZoomLevel, string)[] displayZoomLevels = {
            (Display.ZoomLevel.Normal, "Normal"),
            (Display.ZoomLevel.Percent125, "125%"),
            (Display.ZoomLevel.Percent150, "150%"),
            (Display.ZoomLevel.Percent200, "200%")
        };

        private void DisplayZoomChanged(object? sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var level = displayZoomLevels[displayZoomComboBox.SelectedIndex].Item1;
            session.SetPreference("com.microsoft.windows.display", "zoom", level.ToString().ToLower());
            Close();
        }
    }
}
