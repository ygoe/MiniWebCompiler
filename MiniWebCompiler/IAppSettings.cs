using System.ComponentModel;
using Unclassified.Util;

namespace MiniWebCompiler
{
	public interface IAppSettings : ISettings
	{
		/// <summary>
		/// Provides settings for the main window state.
		/// </summary>
		IWindowStateSettings MainWindowState { get; }

		/// <summary>
		/// Provides settings for the settings window state.
		/// </summary>
		IWindowStateSettings SettingsWindowState { get; }

		/// <summary>
		/// Gets or sets the width of the projects list panel in pixels.
		/// </summary>
		[DefaultValue(200)]
		int ProjectsListWidth { get; set; }

		/// <summary>
		/// Gets or sets the width of the files list panel in pixels.
		/// </summary>
		[DefaultValue(300)]
		int FilesListWidth { get; set; }

		/// <summary>
		/// Gets or sets an array containing the project directories to watch.
		/// </summary>
		string[] ProjectDirectories { get; set; }

		/// <summary>
		/// Gets or sets the project directory that is selected in the UI.
		/// </summary>
		string SelectedProjectDirectory { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether a confirmation message is shown when the user
		/// closes the application window.
		/// </summary>
		[DefaultValue(true)]
		bool AskOnWindowClose { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the application window is hidden to a tray icon
		/// when minimized.
		/// </summary>
		[DefaultValue(true)]
		bool HideOnMinimize { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether a sound is played on successful compilation.
		/// </summary>
		bool PlaySuccessSound { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether a sound is played when a compilation error
		/// occured.
		/// </summary>
		bool PlayErrorSound { get; set; }

		/// <summary>
		/// Gets or sets the default value of the KeepUnminifiedFiles property for new projects.
		/// </summary>
		bool KeepUnminifiedFilesDefault { get; set; }
	}
}
