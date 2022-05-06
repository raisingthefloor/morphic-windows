// BarMultiButtonControl.xaml.cs: Control for Bar images.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Morphic.Client.Bar.UI.BarControls
{
    /// <summary>
    /// Interaction logic for MorphicImage.xaml
    /// </summary>
    public partial class BitmapOrXamlImage : UserControl
    {
        public BitmapOrXamlImage()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register("ImageSource", typeof(ImageSource), typeof(BitmapOrXamlImage), new PropertyMetadata(null, new PropertyChangedCallback(OnImageSourceChanged)));
        public ImageSource? ImageSource 
        {
            get => (ImageSource)GetValue(ImageSourceProperty);
            set
            {
                SetValue(ImageSourceProperty, value);
            }
        }
        //
        private static void OnImageSourceChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var senderAsBitmapOrXamlImage = ((BitmapOrXamlImage)sender);
            senderAsBitmapOrXamlImage.UpdateCurrentContent();
            if (senderAsBitmapOrXamlImage.ImageSourceChanged is not null)
            {
                senderAsBitmapOrXamlImage.ImageSourceChanged(sender, new PropertyChangedEventArgs("ImageSource"));
            }
        }
        //
        public event PropertyChangedEventHandler ImageSourceChanged;

        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register("Stretch", typeof(Stretch), typeof(BitmapOrXamlImage), new PropertyMetadata(Stretch.Uniform, new PropertyChangedCallback(OnStretchChanged)));
        public Stretch Stretch
        {
            get => (Stretch)GetValue(StretchProperty);
            set
            {
                SetValue(StretchProperty, value);
            }
        }
        //
        private static void OnStretchChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ((BitmapOrXamlImage)sender).UpdateCurrentContent();
        }

        public static readonly DependencyProperty XamlContentProperty = DependencyProperty.Register("XamlContent", typeof(Canvas), typeof(BitmapOrXamlImage), new PropertyMetadata(null, new PropertyChangedCallback(OnXamlContentChanged)));
        public Canvas? XamlContent 
        { 
            get => (Canvas)GetValue(XamlContentProperty);
            set
            {
                SetValue(XamlContentProperty, value);
            }
        }
        //
        private static void OnXamlContentChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ((BitmapOrXamlImage)sender).UpdateCurrentContent();
        }

        private void UpdateCurrentContent()
        {
            var imageSource = (ImageSource)GetValue(ImageSourceProperty);
            var xamlContent = (Canvas)GetValue(XamlContentProperty);
            var stretch = (Stretch)GetValue(StretchProperty);

            if (imageSource is not null)
            {
                this.ViewboxContent.Content = new Image()
                {
                    Source = imageSource,
                    Stretch = stretch
                };
            }
            else if (xamlContent is not null)
            {
                this.ViewboxContent.Content = xamlContent;
            }
            else
            {
                this.ViewboxContent.Content = null;
            }
        }
    }
}
