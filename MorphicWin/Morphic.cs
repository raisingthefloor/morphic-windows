using System;
using System.Configuration;
using System.Windows;
using MorphicService;

#nullable enable

namespace MorphicWin
{
    class Morphic
    {

        #region Instance

        internal static Morphic Shared = new Morphic();

        private Morphic()
        {
            var endpointString = ConfigurationManager.AppSettings.Get("MorphicServiceEndpoint");
            Endpoint = new Uri(endpointString);
            PreferencesService = new PreferencesService(Endpoint);
        }

        #endregion Instance

        #region Service

        private Uri Endpoint;
        public PreferencesService PreferencesService { get; }

        #endregion

        #region Quick Strip Window

        private Window? QuickStrip = null;

        public void ToggleQuickStrip()
        {
            if (QuickStrip != null)
            {
                HideQuickStrip();
            }
            else
            {
                ShowQuickStrip();
            }
        }

        public void ShowQuickStrip()
        {
            if (QuickStrip == null)
            {
                QuickStrip = new QuickStrip();
                QuickStrip.Closed += QuickStripClosed;
                var screenSize = SystemParameters.WorkArea;
                QuickStrip.Top = screenSize.Height - QuickStrip.Height;
                QuickStrip.Left = screenSize.Width - QuickStrip.Width;
                QuickStrip.Show();
            }
            QuickStrip.Activate();
        }

        public void HideQuickStrip()
        {
            QuickStrip.Close();
        }

        private void QuickStripClosed(object? sender, EventArgs e)
        {
            QuickStrip = null;
        }

        #endregion

        #region Configurator Window

        private MorphicConfigurator? Configurator;

        internal void OpenConfigurator()
        {
            if (Configurator == null)
            {
                Configurator = new MorphicConfigurator();
                Configurator.Show();
                Configurator.Closed += OnConfiguratorClosed;
            }
            Configurator.Activate();
        }

        private void OnConfiguratorClosed(object? sender, EventArgs e)
        {
            Configurator = null;
        }

        #endregion
    }
}

#nullable disable