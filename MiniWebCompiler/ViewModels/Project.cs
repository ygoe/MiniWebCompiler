using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using fastJSON;
using Microsoft.Win32;
using Unclassified.UI;
using Unclassified.Util;
using ViewModelKit;

namespace MiniWebCompiler.ViewModels
{
	public class Project : ViewModelBase
	{
		#region Private data

		public const string ProjectFileName = "miniwebcompiler.json";

		private bool isLoaded;
		private FileSystemWatcher watcher;
		private DispatcherTimer updateTimeTimer;

		#endregion Private data

		#region Constructor

		public Project(string projectPath)
		{
			ProjectPath = projectPath;
			Name = Path.GetFileName(projectPath);
			LoadFromFile();
			isLoaded = true;

			updateTimeTimer = new DispatcherTimer();
			updateTimeTimer.Interval = TimeSpan.FromSeconds(5);
			updateTimeTimer.Tick += UpdateTimeTimerOnTick;
			updateTimeTimer.Start();

			InitializeAsync();
		}

		private async void InitializeAsync()
		{
			foreach (var file in Files)
			{
				await file.Compile(false);
			}
		}

		#endregion Constructor

		#region Properties

		public string Name { get; set; }

		private void OnNameChanged()
		{
			if (isLoaded)
			{
				SaveToFile();
			}
		}

		public string ProjectPath { get; set; }

		private void OnProjectPathChanged()
		{
			if (watcher != null)
			{
				watcher.Dispose();
				watcher = null;
			}
			if (Directory.Exists(ProjectPath))
			{
				watcher = new FileSystemWatcher(ProjectPath);
				watcher.IncludeSubdirectories = true;
				watcher.Changed += Watcher_Changed;
				watcher.Created += Watcher_Changed;
				watcher.Renamed += Watcher_Changed;
				watcher.EnableRaisingEvents = true;
			}
		}

		private void Watcher_Changed(object sender, FileSystemEventArgs args)
		{
			if (!Application.Current.Dispatcher.CheckAccess())
			{
				Application.Current.Dispatcher.Invoke((Action<object, FileSystemEventArgs>)Watcher_Changed, sender, args);
			}
			else
			{
				foreach (var file in Files)
				{
					if (file.MatchesFile(args.FullPath))
					{
						file.ScheduleCompile();
					}
				}
			}
		}

		public bool KeepIntermediaryFiles { get; set; }

		private void OnKeepIntermediaryFilesChanged()
		{
			if (isLoaded)
			{
				SaveToFile();
			}
		}

		public bool KeepUnminifiedFiles { get; set; }

		private void OnKeepUnminifiedFilesChanged()
		{
			if (isLoaded)
			{
				SaveToFile();
			}
		}

		public ObservableCollection<ProjectFile> Files { get; } = new ObservableCollection<ProjectFile>();

		public ProjectFile SelectedFile { get; set; }

		public bool? Status { get; private set; } = true;

		#endregion Properties

		#region Commands

		public DelegateCommand BrowseProjectPathCommand { get; }

		private void OnBrowseProjectPath()
		{
			var dlg = new OpenFolderDialog();
			if (!string.IsNullOrWhiteSpace(ProjectPath))
				dlg.InitialFolder = ProjectPath;
			dlg.Title = "Select project root folder";
			if (dlg.ShowDialog() == true)
			{
				ProjectPath = dlg.SelectedFolder;
				SaveToFile();
			}
		}

		public DelegateCommand AddFileCommand { get; }

		private async void OnAddFile()
		{
			var dlg = new OpenFileDialog
			{
				CheckFileExists = true,
				Filter = "Web files|*.css;*.js;*.scss|All files|*.*",
				InitialDirectory = ProjectPath,
				Multiselect = true,
				Title = "Select files to compile"
			};
			if (dlg.ShowDialog() == true)
			{
				foreach (string fileName in dlg.FileNames)
				{
					var file = new ProjectFile(this);
					file.FilePath = PathUtil.GetRelativePath(fileName, ProjectPath, false);
					Files.Add(file);
					file.PropertyChanged += File_PropertyChanged;
					SelectedFile = file;
					await file.Compile(false);
				}
				UpdateStatus();
				SortFiles();
				SaveToFile();
			}
		}

		[DependsOn(nameof(SelectedFile))]
		public DelegateCommand RemoveFileCommand { get; }

		private bool CanRemoveFile() => SelectedFile != null;

		private void OnRemoveFile()
		{
			if (SelectedFile != null)
			{
				if (MessageBox.Show("Remove the file \"" + SelectedFile.FilePath + "\" from the project?", App.Name, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
				{
					Files.Remove(SelectedFile);
					UpdateStatus();
					SaveToFile();
				}
			}
		}

		public DelegateCommand CompileAllCommand { get; }

		private void OnCompileAll()
		{
			foreach (var file in Files)
			{
				file.CompileCommand.TryExecute();
			}
		}

		#endregion Commands

		#region Status handling

		private void File_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
		{
			if (args.PropertyName == nameof(ProjectFile.Status))
			{
				UpdateStatus();
			}
		}

		private void UpdateStatus()
		{
			if (Files.Any(f => f.Status == null))
			{
				Status = null;
			}
			else if (Files.Any(f => f.Status == false))
			{
				Status = false;
				SelectedFile = Files.FirstOrDefault(f => f.Status == false);
			}
			else
			{
				Status = true;
			}
		}

		#endregion Status handling

		#region Loading and saving

		private void LoadFromFile()
		{
			string projectFileName = Path.Combine(ProjectPath, ProjectFileName);
			try
			{
				KeepUnminifiedFiles = App.Settings.KeepUnminifiedFilesDefault;

				if (File.Exists(projectFileName))
				{
					Name = "";
					Files.Clear();

					string json;
					try
					{
						json = File.ReadAllText(projectFileName);
					}
					catch (Exception ex)
					{
						MessageBox.Show($"Error reading config file: {ex.Message}", App.Name, MessageBoxButton.OK, MessageBoxImage.Error);
						return;
					}

					if (json.StartsWith("MiniWebCompiler v1"))
					{
						int index = 0;
						using (var reader = new StreamReader(projectFileName))
						{
							while (!reader.EndOfStream)
							{
								string line = reader.ReadLine().Trim();
								index++;
								if (index == 1)
								{
									if (line != "MiniWebCompiler v1")
										return;
								}
								else if (index == 2)
								{
									Name = line;
								}
								else
								{
									var file = new ProjectFile(this);
									file.FilePath = line;
									Files.Add(file);
									file.PropertyChanged += File_PropertyChanged;
								}
							}
						}

						// Save file in new format now
						SaveToFile();
					}
					else
					{
						try
						{
							var configFile = JSON.ToObject<ConfigFile>(json);
							Name = configFile.ProjectName;
							KeepUnminifiedFiles = configFile.KeepUnminifiedFiles;
							KeepIntermediaryFiles = configFile.KeepIntermediaryFiles;
							foreach (var fileEntry in configFile.Files)
							{
								var file = new ProjectFile(this);
								file.FilePath = fileEntry.Name.Replace('/', Path.DirectorySeparatorChar);
								Files.Add(file);
								file.PropertyChanged += File_PropertyChanged;
							}
						}
						catch (Exception ex)
						{
							MessageBox.Show($"Error parsing config file: {ex.Message}", App.Name, MessageBoxButton.OK, MessageBoxImage.Error);
							return;
						}
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("The project file \"" + projectFileName + "\" could not be loaded. " + ex.Message, App.Name, MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void SaveToFile()
		{
			string projectFileName = Path.Combine(ProjectPath, ProjectFileName);

			// Serialize JSON data
			var configFile = new ConfigFile
			{
				ProjectName = Name,
				KeepUnminifiedFiles = KeepUnminifiedFiles,
				KeepIntermediaryFiles = KeepIntermediaryFiles
			};
			configFile.Files.AddRange(Files.Select(f => new ConfigFile.File { Name = f.FilePath.Replace('\\', '/') }));

			string json;
			try
			{
				var jsonParams = new JSONParameters
				{
					FormatterIndentSpaces = 2,
					SerializeToCamelCaseNames = true,
					UseExtensions = false,
					UseEscapedUnicode = false
				};
				json = JSON.ToNiceJSON(configFile, jsonParams);
				json = json.TrimEnd() + Environment.NewLine;
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error serializing config file: {ex.Message}", App.Name, MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			// Create backup of existing file
			try
			{
				if (File.Exists(projectFileName))
				{
					File.Copy(projectFileName, projectFileName + ".bak", true);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error backing up config file: {ex.Message}", App.Name, MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			// Write new config file
			try
			{
				File.WriteAllText(projectFileName, json);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error writing config file (backup created): {ex.Message}", App.Name, MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			// Delete backup file
			try
			{
				File.Delete(projectFileName + ".bak");
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error deleting backup config file: {ex.Message}", App.Name, MessageBoxButton.OK, MessageBoxImage.Error);
			}

			//try
			//{
			//	if (File.Exists(projectFileName))
			//	{
			//		File.Copy(projectFileName, projectFileName + ".bak");
			//	}
			//	using (var writer = new StreamWriter(projectFileName))
			//	{
			//		writer.WriteLine("MiniWebCompiler v1");
			//		writer.WriteLine(Name);
			//		foreach (var file in Files)
			//		{
			//			writer.WriteLine(file.FilePath);
			//		}
			//	}
			//	if (File.Exists(projectFileName + ".bak"))
			//	{
			//		File.Delete(projectFileName + ".bak");
			//	}
			//}
			//catch (Exception ex)
			//{
			//	MessageBox.Show("The project file \"" + projectFileName + "\" could not be saved. " + ex.Message, App.Name, MessageBoxButton.OK, MessageBoxImage.Error);
			//}
		}

		public class ConfigFile
		{
			public string ProjectName { get; set; }

			[JsonConditional]
			public bool KeepUnminifiedFiles { get; set; }

			[JsonConditional]
			public bool KeepIntermediaryFiles { get; set; }

			public List<File> Files { get; set; } = new List<File>();

			public class File
			{
				public string Name { get; set; }
			}
		}

		#endregion Loading and saving

		private void SortFiles()
		{
			var selectedFile = SelectedFile;
			var orderedFiles = Files.OrderBy(f => f.FilePath.ToLowerInvariant()).ToList();
			Files.Clear();
			foreach (var file in orderedFiles)
			{
				Files.Add(file);
			}
			SelectedFile = selectedFile;
		}

		private void UpdateTimeTimerOnTick(object sender, EventArgs args)
		{
			foreach (var file in Files)
			{
				file.RaiseLastCompileTimeStrChanged();
			}
		}
	}
}
