// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under 
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and 
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants 
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant 
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

using System.Windows.Controls;
using Morphic.Settings;

namespace Morphic.Client.QuickStrip
{
    public class TextZoomInHelpControlBuilder: IQuickHelpControlBuilder
    {

        public TextZoomInHelpControlBuilder(Display display)
        {
            Display = display;
        }

        public Display Display { get; }

        public UserControl Build()
        {
            var zoomControl = new QuickHelpTextZoomControl();
            zoomControl.PagerControl.NumberOfPages = Display.NumberOfZoomLevels;
            zoomControl.PagerControl.CurrentPage = Display.NumberOfZoomLevels - 1 - Display.CurrentZoomLevel;
            if (Display.CanZoomIn)
            {
                zoomControl.TitleLabel.Content = Properties.Resources.QuickStrip_Resolution_Bigger_HelpTitle;
                zoomControl.MessageLabel.Content = Properties.Resources.QuickStrip_Resolution_Bigger_HelpMessage;
            }
            else
            {
                zoomControl.TitleLabel.Content = Properties.Resources.QuickStrip_Resolution_Bigger_LimitTitle;
                zoomControl.MessageLabel.Content = Properties.Resources.QuickStrip_Resolution_Bigger_LimitMessage;
            }
            return zoomControl;
        }

    }

    public class TextZoomOutHelpControlBuilder : IQuickHelpControlBuilder
    {

        public TextZoomOutHelpControlBuilder(Display display)
        {
            Display = display;
        }

        public Display Display { get; }

        public UserControl Build()
        {
            var zoomControl = new QuickHelpTextZoomControl();
            zoomControl.PagerControl.NumberOfPages = Display.NumberOfZoomLevels;
            zoomControl.PagerControl.CurrentPage = Display.NumberOfZoomLevels - 1 - Display.CurrentZoomLevel;
            if (Display.CanZoomOut)
            {
                zoomControl.TitleLabel.Content = Properties.Resources.QuickStrip_Resolution_Smaller_HelpTitle;
                zoomControl.MessageLabel.Content = Properties.Resources.QuickStrip_Resolution_Smaller_HelpMessage;
            }
            else
            {
                zoomControl.TitleLabel.Content = Properties.Resources.QuickStrip_Resolution_Smaller_LimitTitle;
                zoomControl.MessageLabel.Content = Properties.Resources.QuickStrip_Resolution_Smaller_LimitMessage;
            }
            return zoomControl;
        }

    }
}
