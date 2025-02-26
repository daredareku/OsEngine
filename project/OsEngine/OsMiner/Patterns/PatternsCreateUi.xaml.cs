﻿using OsEngine.Language;
using System.Windows;


namespace OsEngine.OsMiner.Patterns
{
    /// <summary>
    /// Interaction Logic for PatternsCreateChangeUi.xaml
    /// Логика взаимодействия для PatternsCreateChangeUi.xaml
    /// </summary>
    public partial class PatternsCreateUi : Window
    {
        public PatternsCreateUi(int patternNum)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            TextBoxPatternName.Text = OsLocalization.Miner.Label25 + patternNum;
            Title = OsLocalization.Miner.Label26;
            LabelName.Content = OsLocalization.Miner.Message4;
            ButtonAccept.Content = OsLocalization.Miner.Button1;

            this.Activate();
            this.Focus();
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            IsAccepted = true;
            NamePattern = TextBoxPatternName.Text;
            Close();
        }

        public bool IsAccepted;

        public string NamePattern;
    }
}
