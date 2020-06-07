using Morphic.Settings;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Morphic.ManualTester
{
    /// <summary>
    /// Interaction logic for SolutionHeader.xaml
    /// </summary>
    public partial class SolutionHeader : UserControl
    {
        public SettingsManager manager;
        public Solution solution;
        private MainWindow window;
        private bool itemsLoaded = false;
        public SolutionHeader(MainWindow window, SettingsManager manager, Solution solution)
        {
            InitializeComponent();
            this.window = window;
            this.manager = manager;
            this.solution = solution;
            ControlStack.Header = solution.Id;
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
            if(!itemsLoaded)
            {
                itemsLoaded = true;
                ControlStack.Items.Clear();
                foreach (var setting in solution.Settings)
                {
                    switch (setting.Kind)
                    {
                        case Setting.ValueKind.Boolean:
                            ControlStack.Items.Add(new ManualControlBoolean(window, manager, solution.Id, setting));
                            break;
                        case Setting.ValueKind.Double:
                            ControlStack.Items.Add(new ManualControlDouble(window, manager, solution.Id, setting));
                            break;
                        case Setting.ValueKind.Integer:
                            ControlStack.Items.Add(new ManualControlInteger(window, manager, solution.Id, setting));
                            break;
                        case Setting.ValueKind.String:
                            ControlStack.Items.Add(new ManualControlString(window, manager, solution.Id, setting));
                            break;
                    }
                }
            }
        }

        private void ControlStack_Collapsed(object sender, RoutedEventArgs e)
        {
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
    }
}
