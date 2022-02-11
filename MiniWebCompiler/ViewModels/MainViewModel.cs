using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MiniWebCompiler.Views;
using Unclassified.UI;
using ViewModelKit;

#pragma warning disable IDE0051 // Nicht verwendete private Member entfernen (ViewModelKit command handlers)

namespace MiniWebCompiler.ViewModels
{
	public class MainViewModel : ViewModelBase
	{
		public static MainViewModel Instance { get; private set; }

		private System.Media.SoundPlayer errorSound;
		private System.Media.SoundPlayer successSound;

		public MainViewModel()
		{
			Instance = this;

			ProjectsListWidth = new GridLength(App.Settings?.ProjectsListWidth ?? 100);
			FilesListWidth = new GridLength(App.Settings?.FilesListWidth ?? 200);

			if (App.Settings != null)
			{
				foreach (string projectPath in App.Settings.ProjectDirectories)
				{
					var project = new Project(projectPath);
					Projects.Add(project);
					if (projectPath == App.Settings.SelectedProjectDirectory)
						SelectedProject = project;
					project.PropertyChanged += Project_PropertyChanged;
				}
				SortProjects();
				UpdateStatus();
			}
		}

		#region Properties

		public Cursor Cursor { get; set; } = Cursors.Arrow;

		public ObservableCollection<Project> Projects { get; } = new ObservableCollection<Project>();

		public Project SelectedProject { get; set; }

		private void OnSelectedProjectChanged()
		{
			App.Settings.SelectedProjectDirectory = SelectedProject?.ProjectPath;
		}

		public bool IsProjectSelected => SelectedProject != null;

		public bool? Status { get; set; } = true;

		public GridLength ProjectsListWidth { get; set; }

		private void OnProjectsListWidthChanged()
		{
			if (App.Settings != null && Views.MainWindow.Instance != null)
			{
				if (ProjectsListWidth.Value < 40) ProjectsListWidth = new GridLength(0);
				double maxWidth = Views.MainWindow.Instance.Width - 40 - FilesListWidth.Value - 40;
				if (ProjectsListWidth.Value > maxWidth) ProjectsListWidth = new GridLength(maxWidth);
				App.Settings.ProjectsListWidth = (int)ProjectsListWidth.Value;
			}
		}

		public GridLength FilesListWidth { get; set; }

		private void OnFilesListWidthChanged()
		{
			if (App.Settings != null && Views.MainWindow.Instance != null)
			{
				if (FilesListWidth.Value < 100) FilesListWidth = new GridLength(100);
				double maxWidth = Views.MainWindow.Instance.Width - 40 - ProjectsListWidth.Value - 40;
				if (FilesListWidth.Value > maxWidth) FilesListWidth = new GridLength(maxWidth);
				App.Settings.FilesListWidth = (int)FilesListWidth.Value;
			}
		}

		#endregion Properties

		#region Commands

		public DelegateCommand AddProjectCommand { get; }

		private void OnAddProject()
		{
			var dlg = new OpenFolderDialog
			{
				Title = "Select project root folder"
			};
			if (dlg.ShowDialog() == true)
			{
				var project = new Project(dlg.SelectedFolder);
				Projects.Add(project);
				App.Settings.ProjectDirectories = Projects.Select(p => p.ProjectPath).ToArray();
				SelectedProject = project;
				SortProjects();
				project.PropertyChanged += Project_PropertyChanged;
				UpdateStatus();
			}
		}

		public DelegateCommand RemoveProjectCommand { get; }

		private bool CanRemoveProject() => SelectedProject != null;

		private void OnRemoveProject()
		{
			if (SelectedProject != null)
			{
				var project = SelectedProject;
				if (MessageBox.Show("Remove the project \"" + project.Name + "\"?", App.Name, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
				{
					Projects.Remove(project);
					App.Settings.ProjectDirectories = Projects.Select(p => p.ProjectPath).ToArray();
					UpdateStatus();

					string projectFileName = Path.Combine(project.ProjectPath, Project.ProjectFileName);
					if (MessageBox.Show("Also delete the project file \"" + projectFileName + "\"?", App.Name, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
					{
						try
						{
							File.Delete(projectFileName);
						}
						catch (Exception ex)
						{
							MessageBox.Show("The project file \"" + projectFileName + "\" could not be deleted. " + ex.Message, App.Name, MessageBoxButton.OK, MessageBoxImage.Error);
						}
					}
				}
			}
		}

		public DelegateCommand SettingsCommand { get; }

		private void OnSettings()
		{
			var window = new SettingsWindow
			{
				Owner = MainWindow.Instance
			};
			window.ShowDialog();
		}

		public DelegateCommand AboutCommand { get; }

		private async void OnAbout()
		{
			AboutCommand.IsEnabled = false;
			Cursor = Cursors.AppStarting;

			var sb = new StringBuilder();
			sb.AppendLine("Mini Web Compiler");
			sb.AppendLine(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright);
			sb.AppendLine("Version " + Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
			sb.AppendLine();

			string Indent(string txt) => "    " + txt.TrimEnd().Replace("\n", "\n    ") + Environment.NewLine;

			var babelVersionTask = Exec("babel --version");
			var cssoVersionTask = Exec("csso --version");
			var nodeVersionTask = Exec("node --version");
			var rollupVersionTask = Exec("rollup --version");
			var sassVersionTask = Exec("sass --version");
			var uglifyjsVersionTask = Exec("uglifyjs --version");
			await Task.WhenAll(
				babelVersionTask,
				cssoVersionTask,
				nodeVersionTask,
				rollupVersionTask,
				sassVersionTask,
				uglifyjsVersionTask);
			string babelVersion = await babelVersionTask;
			string cssoVersion = await cssoVersionTask;
			string nodeVersion = await nodeVersionTask;
			string rollupVersion = await rollupVersionTask;
			string sassVersion = await sassVersionTask;
			string uglifyjsVersion = await uglifyjsVersionTask;

			if (rollupVersion.StartsWith("rollup version "))
				rollupVersion = rollupVersion.Substring(15);

			sb.AppendLine("babel:");
			sb.Append(Indent(babelVersion));
			sb.AppendLine("csso:");
			sb.Append(Indent(cssoVersion));
			sb.AppendLine("Node.js:");
			sb.Append(Indent(nodeVersion));
			sb.AppendLine("rollup:");
			sb.Append(Indent(rollupVersion));
			sb.AppendLine("sass:");
			sb.Append(Indent(sassVersion));
			sb.AppendLine("uglifyjs:");
			sb.Append(Indent(uglifyjsVersion));

			AboutCommand.IsEnabled = null;
			Cursor = Cursors.Arrow;
			MessageBox.Show(sb.ToString(), "About");
		}

		public DelegateCommand WebsiteCommand { get; }

		private void OnWebsite()
		{
			Process.Start("https://unclassified.software/apps/miniwebcompiler?ref=app");
		}

		#endregion Commands

		#region Status handling

		private void Project_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
		{
			if (args.PropertyName == nameof(Project.Status))
			{
				UpdateStatus();
			}
		}

		private void UpdateStatus()
		{
			if (Projects.Any(p => p.Status == null))
			{
				Status = null;
			}
			else if (Projects.Any(p => p.Status == false))
			{
				Status = false;
				SelectedProject = Projects.FirstOrDefault(p => p.Status == false);
			}
			else
			{
				Status = true;
				bool justCompiled = Projects.Any(p => p.Files.Any(f => DateTime.UtcNow - f.LastCompileTime < TimeSpan.FromSeconds(5)));
				if (justCompiled)
					PlaySuccessSound();
			}
		}

		#endregion Status handling

		private void SortProjects()
		{
			var selectedProject = SelectedProject;
			var orderedProjects = Projects.OrderBy(p => p.Name.ToLowerInvariant()).ToList();
			Projects.Clear();
			foreach (var project in orderedProjects)
			{
				Projects.Add(project);
			}
			SelectedProject = selectedProject;
		}

		public void PlayErrorSound()
		{
			if (App.Settings.PlayErrorSound)
			{
				try
				{
					if (errorSound == null)
					{
						string soundFile = Path.Combine(App.AppDir, "Error.wav");
						if (File.Exists(soundFile))
						{
							errorSound = new System.Media.SoundPlayer(soundFile);
							errorSound.Load();
						}
					}
					errorSound?.Play();
				}
				catch
				{
				}

				//Console.Beep(1000, 100);
				//Console.Beep(1000, 100);
				//Console.Beep(1000, 100);
			}
		}

		public void PlaySuccessSound()
		{
			if (App.Settings.PlaySuccessSound)
			{
				try
				{
					if (successSound == null)
					{
						string soundFile = Path.Combine(App.AppDir, "Success.wav");
						if (File.Exists(soundFile))
						{
							successSound = new System.Media.SoundPlayer(soundFile);
							successSound.Load();
						}
					}
					successSound?.Play();
				}
				catch
				{
				}
			}
		}

		private async Task<string> Exec(string cmdline)
		{
			// Try to find the command executable file in the application directory, then use that
			string[] parts = cmdline.Split(new[] { ' ' }, 2);
			if (File.Exists(Path.Combine(App.AppDir, parts[0])) ||
				File.Exists(Path.Combine(App.AppDir, parts[0] + ".cmd")))
			{
				cmdline = "\"\"" + App.AppDir + Path.DirectorySeparatorChar + parts[0] + "\" " + parts[1] + "\"";
			}

			var psi = new ProcessStartInfo("cmd", "/c " + cmdline)
			{
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				UseShellExecute = false
			};

			var process = Process.Start(psi);
			await Task.Run(() => process.WaitForExit(5000));
			string stdOut = process.StandardOutput.ReadToEnd();
			return stdOut;
		}
	}
}
