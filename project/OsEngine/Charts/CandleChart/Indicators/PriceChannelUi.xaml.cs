﻿/*
 *Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Interaction logic  for PriceChannelUi.xaml
    /// Логика взаимодействия для PriceChannelUi.xaml
    /// </summary>
    public partial class PriceChannelUi
    {
        /// <summary>
        /// indicator that we're setting up
        /// индикатор который мы настраиваем
        /// </summary>
        private PriceChannel _bollinger;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменились ли настройки у индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="bollinger">configuration indicator/индикатор для настройки</param>
        public PriceChannelUi(PriceChannel bollinger)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _bollinger = bollinger;

            TextBoxLenghtUp.Text = _bollinger.LenghtUpLine.ToString();
            TextBoxLenghtDown.Text = _bollinger.LenghtDownLine.ToString();
            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _bollinger.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _bollinger.ColorDown;
            CheckBoxPaintOnOff.IsChecked = _bollinger.PaintOn;

            ButtonColorUp.Content = OsLocalization.Charts.LabelButtonIndicatorColorUp;
            ButtonColorDown.Content = OsLocalization.Charts.LabelButtonIndicatorColorDown;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorLongPeriod.Content = OsLocalization.Charts.LabelIndicatorLongPeriod;
            LabelIndicatorShortPeriod.Content = OsLocalization.Charts.LabelIndicatorShortPeriod;

            this.Activate();
            this.Focus();
        }

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxLenghtUp.Text) <= 0 ||
                    Convert.ToInt32(TextBoxLenghtDown.Text) <= 0)
                {
                    throw new Exception("error");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _bollinger.ColorUp = HostColorUp.Child.BackColor;
            _bollinger.ColorDown = HostColorDown.Child.BackColor;

            _bollinger.LenghtUpLine = Convert.ToInt32(TextBoxLenghtUp.Text);
            _bollinger.LenghtDownLine = Convert.ToInt32(TextBoxLenghtDown.Text);

            if (CheckBoxPaintOnOff.IsChecked.HasValue)
            {
                _bollinger.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            }

            _bollinger.Save();
            IsChange = true;
            Close();
        }

        /// <summary>
        /// top line color button
        /// кнопка цвет верхней линии
        /// </summary>
        private void ButtonColorUp_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorUp.Child.BackColor;
            dialog.ShowDialog();
            HostColorUp.Child.BackColor = dialog.Color;
        }

        /// <summary>
        /// bottom line color button
        /// кнопка цвет нижней линии
        /// </summary>
        private void ButtonColorDown_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorDown.Child.BackColor;
            dialog.ShowDialog();
            HostColorDown.Child.BackColor = dialog.Color;
        }
    }
}
