// CommunityPickerWindow.xaml.cs: Window to select a community.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Client.Dialogs
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using Core.Community;

    public partial class CommunityPickerWindow : Window
    {
        public List<UserCommunity> Communities { get; set; }

        public UserCommunity? SelectedCommunity;

        public CommunityPickerWindow(IEnumerable<UserCommunity> communities)
        {
            this.Communities = new List<UserCommunity>(communities);
            this.DataContext = this;
            this.InitializeComponent();
        }

        private void Button_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                this.SelectedCommunity = this.Communities.FirstOrDefault(c => c.Id == button.Tag.ToString());
                this.DialogResult = true;
                this.Close();
            }
        }
    }
}

