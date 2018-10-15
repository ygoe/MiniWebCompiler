using System;
using System.Windows;
using Unclassified.UI;
using Unclassified.Util;

namespace MiniWebCompiler.Views
{
	public partial class SettingsWindow : Window
	{
		public SettingsWindow()
		{
			InitializeComponent();

			SettingsHelper.BindWindowState(this, App.Settings.SettingsWindowState);
			this.HideIcon();
		}

		private void CloseButton_Click(object sender, RoutedEventArgs args)
		{
			Close();
		}
	}
}
