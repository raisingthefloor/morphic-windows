using System.Reflection;
using System.Windows;

namespace Morphic.Client.QuickStrip
{
    public partial class AboutMorphicWindow : Window
    {
        public AboutMorphicWindow()
        {
            InitializeComponent();

            var assembly = Assembly.GetExecutingAssembly();
            var informationVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            var location = assembly.Location;
            
            AboutLabel.Content = "Version: " + informationVersion;
        }
    }
}