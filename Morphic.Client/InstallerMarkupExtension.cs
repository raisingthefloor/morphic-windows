using System;
using System.Windows.Markup;

namespace Morphic.Client
{
    public class InstallerMarkupExtension : MarkupExtension
    {
        public string ApplicationName { get; set; } = "Placeholder";

        public string InstalledText { get; set; } = "Uninstall";
        public string NotInstalledText { get; set; } = "Install";

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (InstallerHelper.IsInstalled(ApplicationName))
                return InstalledText;

            return NotInstalledText;
        }
    }
}
