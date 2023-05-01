﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Market;
using System.Windows;

namespace OsEngine.OsTrader.Gui
{
    /// <summary>
    /// Логика взаимодействия для RobotUiLight.xaml
    /// </summary>
    public partial class RobotUiLight : Window
    {
        public RobotUiLight()
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            ServerMaster.SetHostTable(HostPositionOnBoard, HostOrdersOnBoard);
            ServerMaster.GetServers();

            _strategyKeeper = new OsTraderMaster(null,
                null, null, null, null, HostAllPosition,
                null, HostBotLogPrime, null, null, null, null, null,
                null, StartProgram.IsOsTrader);
            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            Closing += TesterUi_Closing;
            Local();

            _painter = new BotTabsPainter(_strategyKeeper, BotsHost);

            _painterServer = new ServerMasterPainter(HostServers, HostServerLog, CheckBoxServerAutoOpen);

            this.Activate();
            this.Focus();

            GlobalGUILayout.Listen(this, "botStationLightUi");

            IsRobotUiLightStart = true;
        }

        ServerMasterPainter _painterServer;

        BotTabsPainter _painter;

        private void Local()
        {
            Title = Title + " " + OsEngine.PrimeSettings.PrimeSettingsMaster.LabelInHeaderBotStation;
            TabItemAllPos.Header = OsLocalization.Trader.Label20;
            TextBoxPositionBord.Header = OsLocalization.Trader.Label21;
            TextBoxPositionAllOrders.Header = OsLocalization.Trader.Label22;
            TabItemLogPrime.Header = OsLocalization.Trader.Label24;
            TabItemControl.Header = OsLocalization.Trader.Label37;
            CheckBoxServerAutoOpen.Content = OsLocalization.Market.Label20;
        }

        void TesterUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label48);
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                e.Cancel = true;
                return;
            }

            _painterServer.Dispose();
            _painter = null;
        }

        private OsTraderMaster _strategyKeeper;

        public static bool IsRobotUiLightStart = false;
    }
}