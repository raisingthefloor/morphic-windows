using System.Windows;
using System.Windows.Controls;

namespace Morphic.ManualTester
{
    using Settings.SettingsHandlers;
    using Settings.SolutionsRegistry;

    /// <summary>
    /// Interaction logic for SolutionHeader.xaml
    /// </summary>
    public partial class SolutionHeader : UserControl
    {
        public Solution solution;
        private MainWindow window;
        private bool itemsLoaded = false;
        public SolutionHeader(MainWindow window, Solution solution)
        {
            InitializeComponent();
            this.window = window;
            this.solution = solution;
            SolutionTitle.Content = solution.SolutionId;
            ControlStack.Items.Add(new TextBlock());
        }

        public void ApplyAllSettings()
        {
            if (!itemsLoaded) return;
            foreach(var element in ControlStack.Items)
            {
                if (element != null)
                {
                    if (element.GetType() == typeof(ManualControlBoolean))
                    {
                        ((ManualControlBoolean)element).ApplySetting();
                    }
                    else if (element.GetType() == typeof(ManualControlInteger))
                    {
                        ((ManualControlInteger)element).ApplySetting();
                    }
                    else if (element.GetType() == typeof(ManualControlDouble))
                    {
                        ((ManualControlDouble)element).ApplySetting();
                    }
                    else if (element.GetType() == typeof(ManualControlString))
                    {
                        ((ManualControlString)element).ApplySetting();
                    }
                }
            }
        }

        private void ControlStack_Expanded(object sender, RoutedEventArgs e)
        {
            RefreshButton.Visibility = Visibility.Visible;
            if(!itemsLoaded)
            {
                itemsLoaded = true;
                ControlStack.Items.Clear();
                foreach (var setting in solution.AllSettings.Values)
                {
                    switch (setting.DataType)
                    {
                        case SettingType.Bool:
                            ControlStack.Items.Add(new ManualControlBoolean(window, setting));
                            break;
                        case SettingType.Real:
                            ControlStack.Items.Add(new ManualControlDouble(window, setting));
                            break;
                        case SettingType.Int:
                            ControlStack.Items.Add(new ManualControlInteger(window, setting));
                            break;
                        case SettingType.String:
                            ControlStack.Items.Add(new ManualControlString(window, setting));
                            break;
                    }
                }
            }
        }

        private void ControlStack_Collapsed(object sender, RoutedEventArgs e)
        {
            RefreshButton.Visibility = Visibility.Hidden;
            if(itemsLoaded)
            {
                foreach(var element in ControlStack.Items)  //check to see if any items require changing, if so they must be preserved
                {
                    if (element != null)
                    {
                        if (element.GetType() == typeof(ManualControlBoolean))
                        {
                            if (((ManualControlBoolean)element).changed) return;
                        }
                        else if (element.GetType() == typeof(ManualControlInteger))
                        {
                            if (((ManualControlInteger)element).changed) return;
                        }
                        else if (element.GetType() == typeof(ManualControlDouble))
                        {
                            if (((ManualControlDouble)element).changed) return;
                        }
                        else if (element.GetType() == typeof(ManualControlString))
                        {
                            if (((ManualControlString)element).changed) return;
                        }
                    }
                }
                itemsLoaded = false;
                ControlStack.Items.Clear();
                ControlStack.Items.Add(new TextBlock());
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            foreach (var element in ControlStack.Items)
            {
                if (element != null)
                {
                    if (element.GetType() == typeof(ManualControlBoolean))
                    {
                        ((ManualControlBoolean)element).CaptureSetting();
                    }
                    else if (element.GetType() == typeof(ManualControlInteger))
                    {
                        ((ManualControlInteger)element).CaptureSetting();
                    }
                    else if (element.GetType() == typeof(ManualControlDouble))
                    {
                        ((ManualControlDouble)element).CaptureSetting();
                    }
                    else if (element.GetType() == typeof(ManualControlString))
                    {
                        ((ManualControlString)element).CaptureSetting();
                    }
                }
            }
        }
    }
}
