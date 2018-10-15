using System;
using System.IO;
using System.Windows;
using MiniWebCompiler.Views;
using Unclassified.Util;

namespace MiniWebCompiler
{
	public partial class App : Application
	{
		public static readonly string AppDir;

		static App()
		{
			AppDir = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
		}

		public const string Name = "Mini Web Compiler";

		private NamedPipeServer pipeServer;

		#region Startup

		protected override void OnStartup(StartupEventArgs args)
		{
			base.OnStartup(args);

			pipeServer = new NamedPipeServer("miniwebcompiler");
			pipeServer.Message += PipeServer_Message;
			pipeServer.Start();

			var view = new MainWindow();
			if (args.Args.Length > 0 && args.Args[0] == "/hide")
			{
				view.WindowState = WindowState.Minimized;
			}
			else
			{
				view.Show();
			}
		}

		#endregion Startup

		#region Settings

		/// <summary>
		/// Provides properties to access the application settings.
		/// </summary>
		public static IAppSettings Settings { get; private set; }

		/// <summary>
		/// Initialises the application settings.
		/// </summary>
		public static void InitializeSettings()
		{
			if (Settings != null) return;   // Already done

			Settings = SettingsAdapterFactory.New<IAppSettings>(
				new FileSettingsStore(
					SettingsHelper.GetAppDataPath(@"Unclassified\MiniWebCompiler", "MiniWebCompiler.conf")));

			// Remember the version of the application.
			// If we need to react on settings changes from previous application versions, here is
			// the place to check the version currently in the settings, before it's overwritten.
			//Settings.LastStartedAppVersion = FL.AppVersion;
		}

		#endregion Settings

		protected override void OnExit(ExitEventArgs args)
		{
			base.OnExit(args);
			pipeServer.Dispose();
		}

		private static async void PipeServer_Message(object sender, NamedPipeServerMessageEventArgs args)
		{
			if (args.Message == "exit")
			{
				try
				{
					await args.RespondAsync("ok");
				}
				catch
				{
					// I don't care whether you receive my response, I'm exiting anyway
				}
				Application.Current.MainWindow.Close();
			}
		}

		public static bool IsSessionEnding { get; private set; }

		private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs args)
		{
			IsSessionEnding = true;
		}
	}
}
