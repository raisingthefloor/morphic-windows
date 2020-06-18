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

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using Morphic.Windows.Native;

namespace Morphic.Client.QuickStrip
{
    public class VolumeUpHelpControlBuilder : IQuickHelpControlBuilder
    {

        public VolumeUpHelpControlBuilder(AudioEndpoint audio)
        {
            Audio = audio;
        }

        public AudioEndpoint Audio { get; }

        public UserControl Build()
        {
            var volumeControl = new QuickHelpVolumeControl();
            var level = Audio.GetMasterVolumeLevel();
            volumeControl.ProgressBar.Value = level;
            volumeControl.PercentageLabel.Content = String.Format("{0:P0}", level);
            if (Audio.GetMasterMuteState())
            {
                volumeControl.ProgressBar.Opacity = 0.5;
                volumeControl.PercentageLabel.Opacity = 0.5;
                volumeControl.TitleLabel.Content = Properties.Resources.QuickStrip_Volume_Unmute_HelpTitle;
                volumeControl.MessageLabel.Content = Properties.Resources.QuickStrip_Volume_Unmute_HelpMessage;
            }
            else
            {
                if (level < 1)
                {
                    volumeControl.TitleLabel.Content = Properties.Resources.QuickStrip_Volume_Up_HelpTitle;
                    volumeControl.MessageLabel.Content = Properties.Resources.QuickStrip_Volume_Up_HelpMessage;
                }
                else
                {
                    volumeControl.TitleLabel.Content = Properties.Resources.QuickStrip_Volume_Up_LimitTitle;
                    volumeControl.MessageLabel.Content = Properties.Resources.QuickStrip_Volume_Up_LimitMessage;
                }
            }
            return volumeControl;
        }

    }

    public class VolumeDownHelpControlBuilder : IQuickHelpControlBuilder
    {

        public VolumeDownHelpControlBuilder(AudioEndpoint audio)
        {
            Audio = audio;
        }

        public AudioEndpoint Audio { get; }

        public UserControl Build()
        {
            var volumeControl = new QuickHelpVolumeControl();
            var level = Audio.GetMasterVolumeLevel();
            volumeControl.ProgressBar.Value = level;
            volumeControl.PercentageLabel.Content = String.Format("{0:P0}", level);
            if (Audio.GetMasterMuteState())
            {
                volumeControl.ProgressBar.Opacity = 0.5;
                volumeControl.PercentageLabel.Opacity = 0.5;
                volumeControl.TitleLabel.Content = Properties.Resources.QuickStrip_Volume_Unmute_HelpTitle;
                volumeControl.MessageLabel.Content = Properties.Resources.QuickStrip_Volume_Unmute_HelpMessage;
            }
            else
            {
                if (level > 0)
                {
                    volumeControl.TitleLabel.Content = Properties.Resources.QuickStrip_Volume_Down_HelpTitle;
                    volumeControl.MessageLabel.Content = Properties.Resources.QuickStrip_Volume_Down_HelpMessage;
                }
                else
                {
                    volumeControl.TitleLabel.Content = Properties.Resources.QuickStrip_Volume_Down_LimitTitle;
                    volumeControl.MessageLabel.Content = Properties.Resources.QuickStrip_Volume_Down_LimitMessage;
                }
            }
            return volumeControl;
        }

    }

    public class VolumeMuteHelpControlBuilder : IQuickHelpControlBuilder
    {

        public VolumeMuteHelpControlBuilder(AudioEndpoint audio)
        {
            Audio = audio;
        }

        public AudioEndpoint Audio { get; }

        public UserControl Build()
        {
            var textControl = new QuickHelpTextControl();
            if (!Audio.GetMasterMuteState())
            {
                textControl.TitleLabel.Content = Properties.Resources.QuickStrip_Volume_Mute_HelpTitle;
                textControl.MessageLabel.Content = Properties.Resources.QuickStrip_Volume_Mute_HelpMessage;
            }
            else
            {
                textControl.TitleLabel.Content = Properties.Resources.QuickStrip_Volume_Mute_MutedTitle;
                textControl.MessageLabel.Content = Properties.Resources.QuickStrip_Volume_Mute_MutedMessage;
            }
            return textControl;
        }

    }
}
