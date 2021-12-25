using System;
using System.Windows;
using MiniWebCompiler.ViewModels;
using Unclassified.Util;

namespace MiniWebCompiler.Views
{
	public partial class MainWindow : Window
	{
		public static MainWindow Instance { get; private set; }

		private readonly System.Windows.Forms.NotifyIcon notifyIcon;
		private readonly System.Drawing.Icon notifyNormalIcon;
		private readonly System.Drawing.Icon notifyWorkingIcon;
		private readonly System.Drawing.Icon notifyErrorIcon;

		#region Constructor

		public MainWindow()
		{
			Instance = this;

			InitializeComponent();

			Title = App.Name;
			Width = 1000;
			Height = 500;
			SettingsHelper.BindWindowState(this, App.Settings.MainWindowState);

			notifyNormalIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.GetCommandLineArgs()[0]);
			var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/MiniWebCompiler;component/MiniWebCompiler_working.ico")).Stream;
			notifyWorkingIcon = new System.Drawing.Icon(iconStream);
			iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/MiniWebCompiler;component/MiniWebCompiler_error.ico")).Stream;
			notifyErrorIcon = new System.Drawing.Icon(iconStream);

			notifyIcon = new System.Windows.Forms.NotifyIcon();
			notifyIcon.MouseClick += NotifyIcon_MouseClick;
			notifyIcon.BalloonTipClicked += NotifyIcon_BalloonTipClicked;
			notifyIcon.Icon = notifyNormalIcon;
			notifyIcon.Text = App.Name;
			notifyIcon.Visible = true;
		}

		#endregion Constructor

		private void Window_DataContextChanged(object sender, DependencyPropertyChangedEventArgs args)
		{
			if (DataContext is MainViewModel vm)
			{
				vm.PropertyChanged += Vm_PropertyChanged;
			}
		}

		private void Vm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
		{
			if (DataContext is MainViewModel vm &&
				args.PropertyName == nameof(Project.Status))
			{
				if (TaskbarItemInfo == null)
				{
					TaskbarItemInfo = new System.Windows.Shell.TaskbarItemInfo();
				}

				if (vm.Status == null)
				{
					TaskbarItemInfo.ProgressValue = 0.08;
					TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
					notifyIcon.Icon = notifyWorkingIcon;
				}
				else if (vm.Status == true)
				{
					TaskbarItemInfo.ProgressValue = 0;
					TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
					notifyIcon.Icon = notifyNormalIcon;
					SetError(null, null);
				}
				else
				{
					TaskbarItemInfo.ProgressValue = 1;
					TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;
					notifyIcon.Icon = notifyErrorIcon;
				}
			}
		}

		private void Window_StateChanged(object sender, EventArgs e)
		{
			if (WindowState == WindowState.Minimized && App.Settings.HideOnMinimize)
			{
				DelayedCall.Start(Hide, 250);
			}
		}

		private void NotifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs args)
		{
			Show();
			WindowState = WindowState.Normal;
		}

		private void NotifyIcon_BalloonTipClicked(object sender, EventArgs args)
		{
			Show();
			WindowState = WindowState.Normal;
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs args)
		{
			if (!App.IsSessionEnding && App.Settings.AskOnWindowClose)
			{
				args.Cancel = MessageBox.Show("Closing the window will terminate Mini Web Compiler and background file processing. To hide the window to a tray icon, minimise it.\n\nDo you want to quit the application?", App.Name, MessageBoxButton.YesNo) != MessageBoxResult.Yes;
			}
		}

		private void Window_Closed(object sender, EventArgs args)
		{
			notifyIcon.Dispose();
		}

		public void SetError(string title, string text)
		{
			if (notifyIcon == null) return;   // Not ready yet

			if (!string.IsNullOrEmpty(title))
			{
				if (string.IsNullOrEmpty(text))
					text = "(no message provided)";
				notifyIcon.ShowBalloonTip(5000, title, text, System.Windows.Forms.ToolTipIcon.Error);
			}
			else
			{
				notifyIcon.Visible = false;
				notifyIcon.Visible = true;
			}
		}
	}
}
