// RestoreWindow.xaml.cs: Restores preferences from a back-up.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Client.Travel
{
    using Microsoft.Extensions.Logging;
    using Morphic.Service;
    using System;
    using System.Windows;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Documents;

    /// <summary>
    /// Select a back-up and restores it.
    /// </summary>
    public partial class RestoreWindow : Window
    {
        private readonly Backups backups;

        public IList<KeyValuePair<string, string>> BackupFiles { get; set; }

        /// <summary>
        /// The display date of the currently selected backup.
        /// </summary>
        public string SelectedBackupDate
        {
            get => (string)this.GetValue(SelectedBackupDateProperty);
            set => this.SetValue(SelectedBackupDateProperty, value);
        }

        public static readonly DependencyProperty SelectedBackupDateProperty = DependencyProperty.Register("SelectedBackupDate", typeof(object), typeof(RestoreWindow), new PropertyMetadata(default(object)));
        public string selectedPath = null!;

        public RestoreWindow(Backups backups)
        {
            this.backups = backups;

            this.DataContext = this;

            this.BackupFiles = this.backups.GetBackups().ToList();
            if (this.BackupFiles.Count > 0)
            {
                // Select the latest backup.
                var (key, value) = this.BackupFiles.Last();
                this.SelectBackup(key, value);
            }
            else
            {
                this.Loaded += (sender, args) =>
                {
                    MessageBox.Show(App.Shared.QuickStripWindow!,
                        "You currently don't have a back-up to restore.\n\nA back-up is created when you apply the settings from your Morphic Vault");
                    this.Close();
                };
            }

            this.InitializeComponent();
        }

        /// <summary>
        /// Click handler for the link showing the selected back-up - show a menu listing the available back-ups.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BackupDateClick(object sender, RoutedEventArgs e)
        {
            ContextMenu menu = new ContextMenu();
            foreach (var (path, date) in this.BackupFiles)
            {
                MenuItem item = new MenuItem()
                {
                    Header = date,
                    Tag = path
                };
                item.Click += this.BackupMenuClick;
                menu.Items.Add(item);
            }

            Rect pos = this.BackupLink.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            pos.Location = this.BackupTextBlock.PointToScreen(pos.Location);
            menu.PlacementRectangle = pos;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        /// <summary>
        /// Called when a back-up has been selected.
        /// </summary>
        /// <param name="path">Path to the back-up.</param>
        /// <param name="name">Display name.</param>
        private void SelectBackup(string path, string name)
        {
            this.SelectedBackupDate = name;
            this.selectedPath = path;
        }

        private void BackupMenuClick(object sender, EventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string path)
            {
                this.SelectBackup(path, item.Header?.ToString()!);
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void OnApply(object sender, RoutedEventArgs e)
        {
            await this.backups.Apply(this.selectedPath);
            MessageBox.Show(this, "Your settings have been restored.", "Morphic");
            this.Close();
        }
    }
}
