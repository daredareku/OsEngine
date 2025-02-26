﻿/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Windows;
using System.Windows.Forms;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Interaction logic  for TickVolumeUi.xaml
    /// Логика взаимодействия для TickVolumeUi.xaml
    /// </summary>
    public partial class TickVolumeUi
    {
        private TickVolume _volume;

        public TickVolumeUi(TickVolume volume) // constructor//конструктор
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _volume = volume;
            ShowSettingsOnForm();

            ButtonColorUp.Content = OsLocalization.Charts.LabelButtonIndicatorColorUp;
            ButtonColorDown.Content = OsLocalization.Charts.LabelButtonIndicatorColorDown;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;

            this.Activate();
            this.Focus();
        }

        private void ShowSettingsOnForm() //upload the settings to form// выгрузить настройки на форму
        {
            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _volume.ColorUp;

            HostColorDown.Child = new TextBox();
            HostColorDown.Child.BackColor = _volume.ColorDown;
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e) //accept// принять
        {
            _volume.ColorUp = HostColorUp.Child.BackColor;
            _volume.ColorDown = HostColorDown.Child.BackColor;
            _volume.Save();
            IsChange = true;
            Close();
        }

        private void ButtonColorUp_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorUp.Child.BackColor;
            dialog.ShowDialog();

            HostColorUp.Child.BackColor = dialog.Color;
        }

        private void ButtonColorDown_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorDown.Child.BackColor;
            dialog.ShowDialog();

            HostColorDown.Child.BackColor = dialog.Color;
        }

        public bool IsChange;
    }
}