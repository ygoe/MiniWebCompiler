using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using fastJSON;
using Unclassified.Util;
using ViewModelKit;

namespace MiniWebCompiler.ViewModels
{
	public class ProjectFile : ViewModelBase
	{
		#region Private data

		private DelayedCall compileDc;
		private bool newError;
		private string fullFileName;
		private string fileDir;
		private Dictionary<string, FileTimeInfo> fileTimes = new Dictionary<string, FileTimeInfo>();
		private bool isAnyFileModified;

		#endregion Private data

		#region Constructor

		public ProjectFile(Project project)
		{
			Project = project;
			compileDc = DelayedCall.Create(async () => await Compile(true), 200);
		}

		#endregion Constructor

		#region Properties

		public Project Project { get; }

		public string FilePath { get; set; }

		public HashSet<string> AdditionalSourceFiles { get; private set; } = new HashSet<string>();

		public bool? Status { get; private set; } = true;

		public string LastLog { get; private set; }

		public DateTime LastCompileTime { get; private set; }

		public string LastCompileTimeStr
		{
			get
			{
				if (LastCompileTime == DateTime.MinValue)
					return "";
				return (DateTime.UtcNow - LastCompileTime).ToVerbose() + " ago";
			}
		}

		public void RaiseLastCompileTimeStrChanged()
		{
			OnPropertyChanged(nameof(LastCompileTimeStr));
		}

		#endregion Properties

		#region Commands

		public DelegateCommand CompileCommand { get; }

		private async void OnCompile()
		{
			await Compile(true);
		}

		#endregion Commands

		#region Public methods

		public bool MatchesFile(string fileName)
		{
			if (PathUtil.PathEquals(fileName, Path.Combine(Project.ProjectPath, FilePath)))
				return true;

			foreach (string addFile in AdditionalSourceFiles)
			{
				if (PathUtil.PathEquals(fileName, addFile))
					return true;
			}
			return false;
		}

		public void ScheduleCompile()
		{
			Status = null;
			compileDc.Reset();
		}

		public async Task Compile(bool force)
		{
			newError = false;
			fullFileName = Path.Combine(Project.ProjectPath, FilePath);
			fileDir = Path.GetDirectoryName(fullFileName);

			if (File.Exists(fullFileName))
			{
				try
				{
					switch (Path.GetExtension(fullFileName))
					{
						case ".css":
							DetectAdditionalSourceFilesCss(fullFileName);
							await CompileCss(force);
							break;
						case ".js":
							DetectAdditionalSourceFilesJavaScript(fullFileName);
							await CompileJavaScript(force);
							break;
						case ".scss":
							DetectAdditionalSourceFilesScss(fullFileName);
							await CompileScss(force);
							break;
						default:
							newError = true;
							Status = false;
							LastLog = "Unsupported file type: " + fullFileName;
							break;
					}
				}
				catch (Exception ex)
				{
					newError = true;
					Status = false;
					LastLog = "Exception while compiling\n\n" + ex.ToString();
				}
			}
			else
			{
				newError = true;
				Status = false;
				LastLog = "Project source file not found: " + fullFileName;
			}

			if (Status != false)
				Status = true;
			if (newError)
			{
				MainViewModel.Instance.PlayErrorSound();
				Views.MainWindow.Instance.SetError(FilePath, LastLog);
			}
		}

		#endregion Public methods

		#region Compiling

		private async Task CompileCss(bool force)
		{
			string cssFileName = Path.GetFileName(fullFileName);
			string minCssFileName = Path.GetFileNameWithoutExtension(fullFileName) + ".min.css";

			if (!force && AreFilesUpToDate(minCssFileName, minCssFileName + ".map"))
			{
				return;
			}
			if (!force) Status = null;

			ClearFileTimes();
			SaveResultFileTime(minCssFileName);
			SaveResultFileTime(minCssFileName + ".map");
			LastLog = "";

			await ExecAsync(
				"csso \"" + cssFileName + "\" \"" + minCssFileName + "\" --map \"" + minCssFileName + ".map\"",
				fileDir);
			PostprocessMapFile(minCssFileName + ".map");

			if (Status != false)
			{
				RestoreResultFileTime(minCssFileName);
				RestoreResultFileTime(minCssFileName + ".map");

				if (isAnyFileModified)
				{
					LastCompileTime = DateTime.UtcNow;
				}

				// TODO: Make optional, or better: use GZipStream and only show the size in the UI
				//await ExecAsync(
				//	"7za a -tgzip \"" + minCssFileName + ".gz\" \"" + minCssFileName + "\" >nul",
				//	Path.GetDirectoryName(fileName));
			}
		}

		private async Task CompileJavaScript(bool force)
		{
			string srcFileName = Path.GetFileName(fullFileName);
			string bundleFileName = Path.GetFileNameWithoutExtension(fullFileName) + ".bundle.js";
			string es5FileName = Path.GetFileNameWithoutExtension(fullFileName) + ".es5.js";
			string minFileName = Path.GetFileNameWithoutExtension(fullFileName) + ".min.js";

			bool transpile = false;

			if (!force && AreFilesUpToDate(minFileName, minFileName + ".map"))
			{
				return;
			}
			if (!force) Status = null;

			ClearFileTimes();
			SaveResultFileTime(minFileName);
			SaveResultFileTime(minFileName + ".map");
			LastLog = "";

			string banner = "";
			string iifeParams = "";
			string iifeArgs = "";
			using (var reader = new StreamReader(fullFileName))
			{
				int lineNumber = 0;
				while (!reader.EndOfStream && lineNumber < 10)
				{
					string line = reader.ReadLine();
					lineNumber++;
					var match = Regex.Match(line, @"^\s*(/\*!.*\*/)");
					if (lineNumber == 1 && match.Success)
					{
						string comment = match.Groups[1].Value;
						banner = " --banner \"" + comment + "\"";
					}
					match = Regex.Match(line, @"^\s*/\*\s*ecmascript\s*\*/", RegexOptions.IgnoreCase);
					if (match.Success)
					{
						transpile = true;
					}
					match = Regex.Match(line, @"^\s*/\*\s*iife-params\((.*)\)\s*\*/", RegexOptions.IgnoreCase);
					if (match.Success)
					{
						iifeParams = match.Groups[1].Value;
					}
					match = Regex.Match(line, @"^\s*/\*\s*iife-args\((.*)\)\s*\*/", RegexOptions.IgnoreCase);
					if (match.Success)
					{
						iifeArgs = match.Groups[1].Value;
					}
				}
			}

			if (AdditionalSourceFiles.Any())
			{
				await ExecAsync(
					"rollup \"" + srcFileName + "\" -o \"" + bundleFileName + "\" -f iife -m" + banner,
					fileDir);

				if ((iifeParams != "" || iifeArgs != "") &&
					File.Exists(Path.Combine(fileDir, bundleFileName)))
				{
					string[] lines = File.ReadAllLines(Path.Combine(fileDir, bundleFileName));
					for (int i = 0; i < 3; i++)
					{
						var match = Regex.Match(lines[i], @"^\(function \(\) \{\s*$");
						if (match.Success)
						{
							lines[i] = lines[i].Replace("()", "(" + iifeParams + ")");
							break;
						}
					}
					for (int i = lines.Length - 1; i > lines.Length - 4; i--)
					{
						var match = Regex.Match(lines[i], @"^\}\(\)\);\s*$");
						if (match.Success)
						{
							lines[i] = lines[i].Replace("()", "(" + iifeArgs + ")");
							break;
						}
					}
					File.WriteAllLines(Path.Combine(fileDir, bundleFileName), lines);
				}
			}
			else
			{
				bundleFileName = srcFileName;
			}

			if (Status != false)
			{
				if (transpile)
				{
					string presets = "";
					string presetDir = Path.Combine(App.AppDir, "node_modules");
					if (!Directory.Exists(presetDir))
					{
						presetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "node_modules");
					}
					//presets = "\"" + Path.Combine(presetDir, "babel-preset-env") + "\",\"" + Path.Combine(presetDir, "babel-preset-minify") + "\"";
					presets = "\"" + Path.Combine(presetDir, "babel-preset-env") + "\"";

					string mapParam = "";
					if (File.Exists(Path.Combine(fileDir, bundleFileName) + ".map"))
					{
						mapParam = "--input-source-map \"" + bundleFileName + ".map\"";
					}
					await ExecAsync(
						"babel \"" + bundleFileName + "\" --out-file \"" + es5FileName + "\" " + mapParam + " --source-maps --presets=" + presets,
						fileDir);

					// TODO: Issues with babel-minify:
					// * Banner/license comment is not preserved
					// * Incorrect results, see https://github.com/babel/minify/issues/797
					// (Add to setup if used)
				}
				else
				{
					es5FileName = bundleFileName;
				}

				{
					string mapParam = "--source-map \"url='" + minFileName + ".map'\"";
					if (File.Exists(Path.Combine(fileDir, es5FileName) + ".map"))
					{
						mapParam = "--source-map \"content='" + es5FileName + ".map',url='" + minFileName + ".map'\"";
					}
					await ExecAsync(
						"uglifyjs " + es5FileName + " --compress --mangle --output \"" + minFileName + "\" --comments=\"/^!/\" " + mapParam,
						fileDir);
					PostprocessMapFile(minFileName + ".map");

					// TODO: Issues with babel + uglify:
					// * Banner/license comment is either not preserved or duplicated (depending on using rollup option --banner)
				}
			}
			if (Status != false)
			{
				RestoreResultFileTime(minFileName);
				RestoreResultFileTime(minFileName + ".map");

				if (!Project.KeepIntermediaryFiles)
				{
					if (bundleFileName != srcFileName)
					{
						File.Delete(Path.Combine(fileDir, bundleFileName));
						File.Delete(Path.Combine(fileDir, bundleFileName + ".map"));
					}
					if (!Project.KeepUnminifiedFiles)
					{
						if (es5FileName != srcFileName)
						{
							File.Delete(Path.Combine(fileDir, es5FileName));
							File.Delete(Path.Combine(fileDir, es5FileName + ".map"));
						}
					}
				}

				if (isAnyFileModified)
				{
					LastCompileTime = DateTime.UtcNow;
				}

				// TODO: Make optional, or better: use GZipStream and only show the size in the UI
				//await ExecAsync(
				//	"7za a -tgzip \"" + minFileName + ".gz\" \"" + minFileName + "\" >nul",
				//	Path.GetDirectoryName(fileName));
			}
		}

		private async Task CompileScss(bool force)
		{
			string scssFileName = Path.GetFileName(fullFileName);
			string cssFileName = Path.GetFileNameWithoutExtension(fullFileName) + ".css";
			string minCssFileName = Path.GetFileNameWithoutExtension(fullFileName) + ".min.css";

			if (!force && AreFilesUpToDate(minCssFileName, minCssFileName + ".map"))
			{
				return;
			}
			if (!force) Status = null;

			ClearFileTimes();
			SaveResultFileTime(minCssFileName);
			SaveResultFileTime(minCssFileName + ".map");
			LastLog = "";

			await ExecAsync(
				"sassc --sourcemap=auto \"" + scssFileName + "\" \"" + cssFileName + "\"",
				fileDir);

			if (Status != false)
			{
				await ExecAsync(
					"csso \"" + cssFileName + "\" \"" + minCssFileName + "\" --map \"" + minCssFileName + ".map\"",
					fileDir);
				PostprocessMapFile(minCssFileName + ".map");
			}
			if (Status != false)
			{
				RestoreResultFileTime(minCssFileName);
				RestoreResultFileTime(minCssFileName + ".map");

				if (!Project.KeepIntermediaryFiles && !Project.KeepUnminifiedFiles)
				{
					File.Delete(Path.Combine(fileDir, cssFileName));
					File.Delete(Path.Combine(fileDir, cssFileName + ".map"));
				}

				if (isAnyFileModified)
				{
					LastCompileTime = DateTime.UtcNow;
				}

				// TODO: Make optional, or better: use GZipStream and only show the size in the UI
				//await ExecAsync(
				//	"7za a -tgzip \"" + minCssFileName + ".gz\" \"" + minCssFileName + "\" >nul",
				//	Path.GetDirectoryName(fileName));
			}
		}

		private void PostprocessMapFile(string mapFileName)
		{
			// Read map file
			string fileName = Path.Combine(fileDir, mapFileName);
			string json = File.ReadAllText(fileName);
			if (!(JSON.Parse(json) is Dictionary<string, object> dict))
				return;   // Unexpected JSON structure

			// Convert absolute paths to relative paths
			if (dict["sources"] is List<object> sources)
			{
				for (int i = 0; i < sources.Count; i++)
				{
					if (sources[i] is string source &&
						Path.IsPathRooted(source))
					{
						sources[i] = PathUtil.GetRelativePath(source, fileDir);
					}
				}
			}
			if (dict.TryGetValue("file", out object fileValue) &&
				fileValue is string fileStr &&
				Path.IsPathRooted(fileStr))
			{
				dict["file"] = PathUtil.GetRelativePath(fileStr, fileDir);
			}

			// Remove sources from map file
			dict.Remove("sourcesContent");

			// Write back file
			json = JSON.ToJSON(dict);
			File.WriteAllText(fileName, json);
		}

		#endregion Compiling

		#region Process helper methods

		private bool AreFilesUpToDate(params string[] outputs)
		{
			string source = Path.Combine(Project.ProjectPath, FilePath);
			string path = Path.GetDirectoryName(source);
			DateTime latestSourceTime = File.GetLastWriteTimeUtc(source);
			foreach (string addFile in AdditionalSourceFiles)
			{
				DateTime addFileTime = File.GetLastWriteTimeUtc(Path.Combine(Project.ProjectPath, addFile));
				if (addFileTime > latestSourceTime)
					latestSourceTime = addFileTime;
			}

			DateTime latestOutputTime = DateTime.MinValue;
			bool allUpToDate = true;
			foreach (string output in outputs)
			{
				DateTime outputTime = File.GetLastWriteTimeUtc(Path.Combine(path, output));
				if (outputTime > latestOutputTime)
				{
					latestOutputTime = outputTime;
				}
				if (!File.Exists(Path.Combine(path, output)) || outputTime < latestSourceTime)
				{
					allUpToDate = false;
				}
			}
			if (LastCompileTime == DateTime.MinValue && latestOutputTime != DateTime.MinValue)
			{
				LastCompileTime = latestOutputTime;
			}
			return allUpToDate;
		}

		private async Task ExecAsync(string cmdline, string directory)
		{
			// Try to find the command executable file in the application directory, then use that
			string[] parts = cmdline.Split(new[] { ' ' }, 2);
			if (File.Exists(Path.Combine(App.AppDir, parts[0] + ".exe")) ||
				File.Exists(Path.Combine(App.AppDir, parts[0] + ".cmd")))
			{
				cmdline = "\"\"" + App.AppDir + Path.DirectorySeparatorChar + parts[0] + "\" " + parts[1] + "\"";
			}
			LastLog += cmdline + Environment.NewLine + Environment.NewLine;

			var psi = new ProcessStartInfo("cmd", "/c " + cmdline)
			{
				CreateNoWindow = true,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				UseShellExecute = false,
				WorkingDirectory = directory
			};

			var process = Process.Start(psi);
			await Task.Run(() => process.WaitForExit(10000));
			string stdErr = process.StandardError.ReadToEnd();
			string stdOut = process.StandardOutput.ReadToEnd();
			LastLog += stdErr + stdOut + Environment.NewLine;
			if (!process.HasExited)
			{
				LastLog += "Process has not exited" + Environment.NewLine;
				newError = true;
				Status = false;
			}
			else if (process.ExitCode != 0)
			{
				newError = true;
				Status = false;
			}
		}

		#endregion Process helper methods

		#region Additional source files detection

		private void DetectAdditionalSourceFilesCss(string fileName, bool clear = true)
		{
			if (clear)
				AdditionalSourceFiles.Clear();
			if (!File.Exists(fileName))
				return;
			string filePath = Path.GetDirectoryName(fileName);
			using (var reader = new StreamReader(fileName))
			{
				while (!reader.EndOfStream)
				{
					string line = reader.ReadLine();
					var match = Regex.Match(line, @"^\s*@import\s+url\(\s*([""']?)(.+?)\1\s*\)");
					if (!match.Success)
					{
						match = Regex.Match(line, @"^\s*@import\s+(""|')(.+?)\1");
					}
					if (match.Success)
					{
						string file = match.Groups[2].Value.Trim();
						if (!file.StartsWith("http://") && !file.StartsWith("https://"))
						{
							file = file.Replace("/", "\\");
							file = Path.Combine(filePath, file);
							if (AdditionalSourceFiles.Add(file))
							{
								DetectAdditionalSourceFilesCss(file, clear: false);
							}
						}
					}
				}
			}
		}

		private void DetectAdditionalSourceFilesJavaScript(string fileName, bool clear = true)
		{
			if (clear)
				AdditionalSourceFiles.Clear();
			if (!File.Exists(fileName))
				return;
			string filePath = Path.GetDirectoryName(fileName);
			using (var reader = new StreamReader(fileName))
			{
				while (!reader.EndOfStream)
				{
					string line = reader.ReadLine();
					var match = Regex.Match(line, @"^\s*import\s+.*([""'])(.+?)\1");
					if (match.Success)
					{
						string file = match.Groups[2].Value.Trim();
						if (Path.GetExtension(file) == "")
							file += ".js";
						file = file.Replace("/", "\\");
						file = Path.Combine(filePath, file);
						if (AdditionalSourceFiles.Add(file))
						{
							DetectAdditionalSourceFilesJavaScript(file, clear: false);
						}
					}
				}
			}
		}

		private void DetectAdditionalSourceFilesScss(string fileName, bool clear = true)
		{
			if (clear)
				AdditionalSourceFiles.Clear();
			if (!File.Exists(fileName))
				return;
			string filePath = Path.GetDirectoryName(fileName);
			using (var reader = new StreamReader(fileName))
			{
				while (!reader.EndOfStream)
				{
					string line = reader.ReadLine();
					var match = Regex.Match(line, @"^\s*@import\s+(?:(""|')(.+?)\1\s*,?\s*)+\s*;\s*$");
					if (match.Success)
					{
						foreach (Capture capture in match.Groups[2].Captures)
						{
							string file = capture.Value.Trim();
							file = file.Replace("/", "\\");
							file = Path.Combine(filePath, file);
							if (Path.GetExtension(file) == "")
							{
								file += ".scss";
							}
							if (Path.GetExtension(file) == ".sass" || Path.GetExtension(file) == ".scss")
							{
								string partialFile = Path.Combine(Path.GetDirectoryName(file), "_" + Path.GetFileName(file));
								if (!File.Exists(file) && File.Exists(partialFile))
									file = partialFile;
								if (AdditionalSourceFiles.Add(file))
								{
									DetectAdditionalSourceFilesScss(file, clear: false);
								}
							}
						}
					}
				}
			}
		}

		#endregion Additional source files detection

		#region File write time and hash methods

		private void ClearFileTimes()
		{
			fileTimes.Clear();
			isAnyFileModified = false;
		}

		private void SaveResultFileTime(string fileName)
		{
			if (File.Exists(Path.Combine(fileDir, fileName)))
			{
				var info = new FileTimeInfo();
				info.LastWriteTimeUtc = File.GetLastWriteTimeUtc(Path.Combine(fileDir, fileName));
				var sha = SHA256.Create();
				using (var fileStream = File.OpenRead(Path.Combine(fileDir, fileName)))
				{
					info.Hash = sha.ComputeHash(fileStream);
				}
				fileTimes[fileName] = info;
			}
		}

		private void RestoreResultFileTime(string fileName)
		{
			if (File.Exists(Path.Combine(fileDir, fileName)) && fileTimes.TryGetValue(fileName, out FileTimeInfo info))
			{
				byte[] newFileHash;
				var sha = SHA256.Create();
				using (var fileStream = File.OpenRead(Path.Combine(fileDir, fileName)))
				{
					newFileHash = sha.ComputeHash(fileStream);
				}
				if (newFileHash.SequenceEqual(info.Hash))
				{
					File.SetLastWriteTimeUtc(Path.Combine(fileDir, fileName), info.LastWriteTimeUtc);
					return;
				}
			}
			isAnyFileModified = true;
		}

		private class FileTimeInfo
		{
			public DateTime LastWriteTimeUtc { get; set; }

			public byte[] Hash { get; set; }
		}

		#endregion File write time and hash methods
	}
}
