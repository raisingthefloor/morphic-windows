using System.IO;
using System.Windows;
using System.Text.Json;

namespace Morphic.Client.QuickStrip
{
    public partial class AboutMorphicWindow : Window
    {
        public AboutMorphicWindow()
        {
            InitializeComponent();
            
            var jsonData = File.ReadAllText("build-info.json");
            
            var doc = JsonDocument.Parse(jsonData).RootElement;

            AboutText.Text = "Version: " + doc.GetProperty("version").GetString();
            AboutText.Text += "\nBuild Time: " + doc.GetProperty("buildTime").GetString();
            AboutText.Text += "\nGit commit: " + doc.GetProperty("commit").GetString();
            //AboutLabel.Text = jsonData;
        }
    }
}