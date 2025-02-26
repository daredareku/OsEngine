﻿/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Windows.Forms;

namespace OsEngine.Alerts
{
    /// <summary>
    /// Interaction logic for AlertMessageUi.xaml
    /// Логика взаимодействия для AlertMessageUi.xaml
    /// </summary>
    public partial class AlertMessageFullUi
    {
        public AlertMessageFullUi(DataGridView grid)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            HostAlerts.Child = grid;
            HostAlerts.Child.Show();
            Title = OsLocalization.Alerts.TitleAlertMessageFullUi;

            OsLocalization.LocalizationTypeChangeEvent += delegate { Title = OsLocalization.Alerts.TitleAlertMessageFullUi; };

            this.Activate();
            this.Focus();
        }
    }
}
