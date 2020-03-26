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
