namespace Morphic.ManualTester
{
    using System.Windows;
    using System.Windows.Controls;
    using Settings.SettingsHandlers;
    using Settings.SolutionsRegistry;

    /// <summary>
    /// Interaction logic for SolutionHeader.xaml
    /// </summary>
    public partial class SolutionHeader : UserControl
    {
        private bool itemsLoaded;
        public Solution Solution { get; }
        private readonly MainWindow window;

        public bool IsExpanded
        {
            get => this.ControlStack.IsExpanded;
            set => this.ControlStack.IsExpanded = value;
        }

        public SolutionHeader(MainWindow window, Solution solution)
        {
            this.InitializeComponent();
            this.window = window;
            this.Solution = solution;
            this.SolutionTitle.Content = solution.SolutionId;
            this.ControlStack.Items.Add(new TextBlock());
        }

        public void ApplyAllSettings()
        {
            if (!this.itemsLoaded)
            {
                return;
            }

            foreach (object? element in this.ControlStack.Items)
            {
                if (element is not null)
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
            this.RefreshButton.Visibility = Visibility.Visible;
            if (!this.itemsLoaded)
            {
                this.itemsLoaded = true;
                this.ControlStack.Items.Clear();
                foreach (var setting in this.Solution.AllSettings.Values)
                {
                    switch (setting.DataType)
                    {
                        case Morphic.Settings.SettingsHandlers.SettingType.Bool:
                            this.ControlStack.Items.Add(new ManualControlBoolean(this.window, setting));
                            break;
                        case Morphic.Settings.SettingsHandlers.SettingType.Real:
                            this.ControlStack.Items.Add(new ManualControlDouble(this.window, setting));
                            break;
                        case Morphic.Settings.SettingsHandlers.SettingType.Int:
                            this.ControlStack.Items.Add(new ManualControlInteger(this.window, setting));
                            break;
                        case Morphic.Settings.SettingsHandlers.SettingType.String:
                            this.ControlStack.Items.Add(new ManualControlString(this.window, setting));
                            break;
                    }
                }
            }
        }

        private void ControlStack_Collapsed(object sender, RoutedEventArgs e)
        {
            this.RefreshButton.Visibility = Visibility.Hidden;
            if (this.itemsLoaded)
            {
                foreach (object? element in this.ControlStack.Items
                ) //check to see if any items require changing, if so they must be preserved
                {
                    if (element is not null)
                    {
                        if (element.GetType() == typeof(ManualControlBoolean))
                        {
                            if (((ManualControlBoolean)element).changed)
                            {
                                return;
                            }
                        }
                        else if (element.GetType() == typeof(ManualControlInteger))
                        {
                            if (((ManualControlInteger)element).changed)
                            {
                                return;
                            }
                        }
                        else if (element.GetType() == typeof(ManualControlDouble))
                        {
                            if (((ManualControlDouble)element).changed)
                            {
                                return;
                            }
                        }
                        else if (element.GetType() == typeof(ManualControlString))
                        {
                            if (((ManualControlString)element).changed)
                            {
                                return;
                            }
                        }
                    }
                }

                this.itemsLoaded = false;
                this.ControlStack.Items.Clear();
                this.ControlStack.Items.Add(new TextBlock());
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            foreach (object? element in this.ControlStack.Items)
            {
                if (element is not null)
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
