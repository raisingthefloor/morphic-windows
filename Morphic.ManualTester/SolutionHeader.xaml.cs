namespace Morphic.ManualTester
{
    using System.Collections.Generic;
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
        private Dictionary<SettingGroup, List<IManualControlEntry>> groups;

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
            this.groups = new Dictionary<SettingGroup, List<IManualControlEntry>>();
            foreach (SettingGroup group in solution.SettingGroups)
            {
                this.groups.Add(group, new List<IManualControlEntry>());
            }
        }

        public void ApplyAllSettings()
        {
            if (!this.itemsLoaded)
            {
                return;
            }

            foreach (object? element in this.ControlStack.Items)
            {
                if (element != null)
                {
                    IManualControlEntry? ielement = element as IManualControlEntry;
                    if(ielement != null)
                    {
                        ielement.ApplySetting();
                    }
                    /*
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
                    */
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
                    IManualControlEntry? entry = null;
                    switch (setting.DataType)
                    {
                        case SettingType.Bool:
                            entry = new ManualControlBoolean(this.window, setting);
                            break;
                        case SettingType.Real:
                            entry = new ManualControlDouble(this.window, setting);
                            break;
                        case SettingType.Int:
                            entry = new ManualControlInteger(this.window, setting);
                            break;
                        case SettingType.String:
                            entry = new ManualControlString(this.window, setting);
                            break;
                    }
                    if(entry != null)
                    {
                        this.ControlStack.Items.Add(entry);
                        if(groups.ContainsKey(setting.SettingGroup))
                        {
                            groups[setting.SettingGroup].Add(entry);
                        }
                    }
                }
                this.RefreshSettings();
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
                    if (element != null)
                    {
                        IManualControlEntry? ielement = element as IManualControlEntry;
                        if (ielement != null)
                        {
                            if(ielement.isChanged())
                            {
                                return;
                            }
                        }
                    }
                }

                this.itemsLoaded = false;
                foreach (List<IManualControlEntry> list in groups.Values)
                {
                    list.Clear();
                }
                this.ControlStack.Items.Clear();
                this.ControlStack.Items.Add(new TextBlock());
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            this.RefreshSettings();
        }

        public async void RefreshSettings()
        {
            if(!this.itemsLoaded)
            {
                return; //no sense refreshing if nothing's out
            }
            foreach (object? element in this.ControlStack.Items)
            {
                if (element != null)
                {
                    IManualControlEntry? ielement = element as IManualControlEntry;
                    if (ielement != null)
                    {
                        ielement.SetLoading();
                    }
                }
            }
            foreach (SettingGroup group in groups.Keys)
            {
                var data = await group.GetAllAsync();
                if (data.Item1.IsSuccess)
                {
                    foreach (object? element in this.ControlStack.Items)
                    {
                        if (element != null)
                        {
                            IManualControlEntry? ielement = element as IManualControlEntry;
                            if (ielement != null)
                            {
                                ielement.ReadCapture(data.Item2);
                            }
                        }
                    }
                    /*
                    foreach (IManualControlEntry entry in groups[group])
                    {
                        entry.ReadCapture(data.Item2);
                    }
                    */
                }
            }
            /*
            foreach (object? element in this.ControlStack.Items)
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
            */
        }

        public void SaveCSV(MainWindow main)
        {
            string[] line = { this.Solution.SolutionId, "---", "---", "---", "----" };
            bool first = true;
            foreach (object? element in this.ControlStack.Items)
            {
                if (element != null)
                {
                    if (element.GetType() == typeof(ManualControlBoolean))
                    {
                        if (first)
                        {
                            first = false;
                            main.AddCSVLine(line);
                        }
                        ((ManualControlBoolean)element).SaveCSV(main);
                    }
                    else if (element.GetType() == typeof(ManualControlInteger))
                    {
                        if (first)
                        {
                            first = false;
                            main.AddCSVLine(line);
                        }
                        ((ManualControlInteger)element).SaveCSV(main);
                    }
                    else if (element.GetType() == typeof(ManualControlDouble))
                    {
                        if (first)
                        {
                            first = false;
                            main.AddCSVLine(line);
                        }
                        ((ManualControlDouble)element).SaveCSV(main);
                    }
                    else if (element.GetType() == typeof(ManualControlString))
                    {
                        if (first)
                        {
                            first = false;
                            main.AddCSVLine(line);
                        }
                        ((ManualControlString)element).SaveCSV(main);
                    }
                }
            }
        }
    }
}
