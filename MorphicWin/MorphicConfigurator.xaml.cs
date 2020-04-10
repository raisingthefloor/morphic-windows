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
using System.Threading.Tasks;
using MorphicCore;
using MorphicService;

namespace MorphicWin
{
    /// <summary>
    /// Interaction logic for MorphicConfigurator.xaml
    /// </summary>
    public partial class MorphicConfigurator : Window
    {
        public MorphicConfigurator(Session session)
        {
            this.session = session;
            InitializeComponent();
        }

        private readonly Session session;

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Settings.Default.UserId == "")
            {
                createUserButton.Visibility = Visibility.Visible;
                clearUserButton.Visibility = Visibility.Hidden;
            }
            else
            {
                createUserButton.Visibility = Visibility.Hidden;
                clearUserButton.Visibility = Visibility.Visible;
            }
        }

        private void CreateTestUser(object? sender, RoutedEventArgs e)
        {
            var task = session.RegisterUser();
            createUserButton.IsEnabled = false;
            task.ContinueWith(task =>
            {
                if (task.Result)
                {
                    if (session.User != null)
                    {
                        Settings.Default.UserId = session.User.Id;
                        Settings.Default.Save();
                    }
                    Close();
                }
                else
                {
                    createUserButton.IsEnabled = true;
                }

            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void ClearTestUser(object? sender, RoutedEventArgs e)
        {
            session.Signout();
            Settings.Default.UserId = "";
            Settings.Default.Save();
            Close();
        }
    }
}
