using System;
using System.Threading.Tasks;
using System.Windows;
using Unclassified.Util;

namespace MiniWebCompiler
{
	internal class Program
	{
		/// <summary>
		/// Application entry point.
		/// </summary>
		/// <remarks>
		/// The App class is set to the build action "ApplicationDefinition" which also generates a
		/// Main method suitable as application entry point. Therefore, this class must be selected
		/// as start object in the project configuration. If the App class was set up otherwise,
		/// Visual Studio would not find the application-wide resources in the App.xaml file and
		/// mark all such StaticResource occurences in XAML files as an error.
		/// </remarks>
		[STAThread]
		public static int Main(string[] args)
		{
			if (args.Length > 0 && args[0] == "/exit")
			{
				return CloseRunningInstance().GetAwaiter().GetResult();
			}

			App.InitializeSettings();

			// Make sure the settings are properly saved in the end
			AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

			// Keep the setup away
			GlobalMutex.Create("Unclassified.MiniWebCompiler");
			if (!GlobalMutex.Instance.TryWait(0))
			{
				MessageBox.Show("Another instance is already running.", App.Name, MessageBoxButton.OK, MessageBoxImage.Information);
				Environment.Exit(1);
			}

			var app = new App();
			app.InitializeComponent();
			app.Run();

			return 0;
		}

		private static async Task<int> CloseRunningInstance()
		{
			using (var pipeClient = new NamedPipeClient("miniwebcompiler", 0))
			{
				if (pipeClient.IsConnected)
				{
					string response = await pipeClient.SendWithResponseAsync("exit", 1000);
					if (response != "ok") return 1;
					await Task.Delay(1000);
				}
			}
			return 0;
		}

		/// <summary>
		/// Called when the current process exits.
		/// </summary>
		/// <remarks>
		/// The processing time in this event is limited. All handlers of this event together must
		/// not take more than ca. 3 seconds. The processing will then be terminated.
		/// </remarks>
		private static void CurrentDomain_ProcessExit(object sender, EventArgs args)
		{
			if (App.Settings != null)
			{
				App.Settings.SettingsStore.Dispose();
			}
		}
	}
}
