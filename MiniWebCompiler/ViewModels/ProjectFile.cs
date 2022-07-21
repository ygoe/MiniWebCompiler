using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using fastJSON;
using Unclassified.Util;
using ViewModelKit;

namespace MiniWebCompiler.ViewModels
{
	public class ProjectFile : ViewModelBase
	{
		#region Private data

		private readonly DelayedCall compileDc;
		private readonly Dictionary<string, FileTimeInfo> fileTimes = new Dictionary<string, FileTimeInfo>();
		private bool newError;
		private string fullFileName;
		private string fileDir;
		private bool isAnyFileModified;
		private bool isCompiling;
		private bool needsRecompile;

		#endregion Private data

		#region Constructor

		public ProjectFile(Project project)
		{
			Project = project;
			if (project.HasBaseline)
			{
				CompressedResultSizeBaseline = CompressedResultSize;
			}
			compileDc = DelayedCall.Create(async () => await Compile(true), 500);
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

		public long CompressedResultSize { get; private set; }

		public string CompressedResultSizeStr
		{
			get
			{
				if (CompressedResultSize < 0)
					return "";
				if (CompressedResultSize < 1024 || App.Settings.FileSizesInBytes)
					return $"{CompressedResultSize:#,##0} B";
				if (CompressedResultSize < 1024 * 10)
					return $"{(double)CompressedResultSize / 1024:0.00} KiB";
				if (CompressedResultSize < 1024 * 100)
					return $"{(double)CompressedResultSize / 1024:0.0} KiB";
				if (CompressedResultSize < 1024 * 1024)
					return $"{(double)CompressedResultSize / 1024:0} KiB";
				if (CompressedResultSize < 1024 * 1024 * 10)
					return $"{(double)CompressedResultSize / 1024 / 1024:0.00} MiB";
				if (CompressedResultSize < 1024 * 1024 * 100)
					return $"{(double)CompressedResultSize / 1024 / 1024:0.0} MiB";
				return $"{(double)CompressedResultSize / 1024 / 1024:0} MiB";
			}
		}

		public void UpdateCompressedResultSizeStr()
		{
			OnPropertyChanged(nameof(CompressedResultSizeStr));
		}

		public long CompressedResultSizeBaseline { get; set; } = -1;

		public string CompressedResultSizeDiffStr
		{
			get
			{
				if (CompressedResultSizeBaseline < 0)
					return "";
				long diff = CompressedResultSize - CompressedResultSizeBaseline;
				if (diff < 0)
					return $"−{-diff:#,##0}";
				if (diff > 0)
					return $"+{diff:#,##0}";
				return $"±0";
			}
		}

		public Visibility BaselineSetVisibility =>
			CompressedResultSizeBaseline >= 0 ? Visibility.Visible : Visibility.Collapsed;

		#endregion Properties

		#region Commands

		public DelegateCommand CompileCommand { get; }

		private async void OnCompile()
		{
			Status = null;
			await Compile(true);
		}

		#endregion Commands

		#region Public methods

		/// <summary>
		/// </summary>
		/// <param name="fileName">The changed file path.</param>
		/// <returns></returns>
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
			Debug.WriteLine("Schedule Compile: " + FilePath);
			Status = null;
			compileDc.Reset();
		}

		public async Task Compile(bool force)
		{
			if (isCompiling)
			{
				Debug.WriteLine("Already compiling: " + FilePath);
				needsRecompile = true;
				return;
			}

			Debug.WriteLine("Begin Compile: " + FilePath);
			Status = null;
			isCompiling = true;
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
							LastLog += (!string.IsNullOrEmpty(LastLog) ? "\n\n" : "") +
								"Unsupported file type: " + fullFileName;
							break;
					}
				}
				catch (Exception ex)
				{
					newError = true;
					Status = false;
					LastLog += (!string.IsNullOrEmpty(LastLog) ? "\n\n" : "") +
						"Exception while compiling\n\n" + ex.ToString();
				}
			}
			else
			{
				newError = true;
				Status = false;
				LastLog += (!string.IsNullOrEmpty(LastLog) ? "\n\n" : "") +
					"Project source file not found: " + fullFileName;
			}

			Debug.WriteLine("End Compile: " + FilePath);
			isCompiling = false;
			if (needsRecompile)
			{
				Debug.WriteLine("Needs Recompile: " + FilePath);
				needsRecompile = false;
				ScheduleCompile();
			}
			else
			{
				if (Status != false)
					Status = true;
				if (newError)
				{
					MainViewModel.Instance.PlayErrorSound();
					Views.MainWindow.Instance.SetError(FilePath, LastLog);
				}
				else
				{
					// Run custom script
					if (isAnyFileModified || force)
					{
						string scriptFile = "miniwebcompiler.cmd";
						if (File.Exists(Path.Combine(Project.ProjectPath, scriptFile)))
						{
							await ExecAsync(
								scriptFile + " \"" + FilePath + "\"",
								Project.ProjectPath);
						}
					}
				}
			}
		}

		#endregion Public methods

		#region Compiling

		private async Task CompileCss(bool force)
		{
			string cssFileName = Path.GetFileName(fullFileName);
			string minCssFileName = Path.GetFileNameWithoutExtension(fullFileName) + ".min.css";

			string buildDir = "";
			using (var reader = new StreamReader(fullFileName))
			{
				int lineNumber = 0;
				while (!reader.EndOfStream && lineNumber < 10)
				{
					string line = reader.ReadLine();
					lineNumber++;
					var match = Regex.Match(line, @"^\s*/\*\s*build-dir\((.*)\)\s*\*/", RegexOptions.IgnoreCase);
					if (match.Success && buildDir == "")
					{
						buildDir = match.Groups[1].Value.Trim().Replace('/', '\\');
						if (buildDir != "" && !buildDir.EndsWith("\\"))
							buildDir += "\\";
						if (buildDir != "")
						{
							minCssFileName = Path.Combine(buildDir, minCssFileName);

							Directory.CreateDirectory(Path.Combine(fileDir, buildDir));
						}
					}
				}
			}

			if (!force && AreFilesUpToDate(minCssFileName, minCssFileName + ".map"))
			{
				SetCompressedResultSize(Path.Combine(fileDir, minCssFileName));
				return;
			}
			if (!force) Status = null;

			ClearFileTimes();
			SaveResultFileTime(minCssFileName);
			SaveResultFileTime(minCssFileName + ".map");
			LastLog = "";
			CompressedResultSize = -1;

			await ExecAsync(
				"csso \"" + cssFileName + "\" --output \"" + minCssFileName + "\" --source-map \"" + minCssFileName + ".map\"",
				fileDir);
			PostprocessMapFile(minCssFileName + ".map");
			if (needsRecompile) return;   // Abort this run and restart

			if (Status != false)
			{
				RestoreResultFileTime(minCssFileName);
				RestoreResultFileTime(minCssFileName + ".map");

				LastLog += Environment.NewLine + "Compiled files:" + Environment.NewLine;
				LastLog += "- " + minCssFileName + Environment.NewLine;

				if (isAnyFileModified)
				{
					LastCompileTime = DateTime.UtcNow;
				}
				else
				{
					LastLog += "Note: File content has not changed to previous build." + Environment.NewLine;
				}

				SetCompressedResultSize(Path.Combine(fileDir, minCssFileName));
			}
		}

		private async Task CompileJavaScript(bool force)
		{
			string srcFileName = Path.GetFileName(fullFileName);
			string bundleFileName = Path.GetFileNameWithoutExtension(fullFileName) + ".bundle.js";
			string minFileName = Path.GetFileNameWithoutExtension(fullFileName) + ".min.js";
			string onlyMinFileName = minFileName;

			string banner = "";
			string iifeParams = "";
			string iifeArgs = "";
			bool noIife = false;
			string buildDir = "";
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
					match = Regex.Match(line, @"^\s*/\*\s*no-iife\s*\*/", RegexOptions.IgnoreCase);
					if (match.Success)
					{
						noIife = true;
					}
					match = Regex.Match(line, @"^\s*/\*\s*build-dir\((.*)\)\s*\*/", RegexOptions.IgnoreCase);
					if (match.Success && buildDir == "")
					{
						buildDir = match.Groups[1].Value.Trim().Replace('/', '\\');
						buildDir = Regex.Replace(buildDir, "\\+", "\\");
						if (buildDir != "" && !buildDir.EndsWith("\\"))
							buildDir += "\\";
						if (buildDir != "")
						{
							bundleFileName = Path.Combine(buildDir, bundleFileName);
							minFileName = Path.Combine(buildDir, minFileName);
							// onlyMinFileName is not changed

							Directory.CreateDirectory(Path.Combine(fileDir, buildDir));
						}
					}
				}
			}

			if (!force && AreFilesUpToDate(minFileName, minFileName + ".map"))
			{
				SetCompressedResultSize(Path.Combine(fileDir, minFileName));
				return;
			}
			if (!force) Status = null;

			ClearFileTimes();
			SaveResultFileTime(minFileName);
			SaveResultFileTime(minFileName + ".map");
			LastLog = "";

			if (AdditionalSourceFiles.Any())
			{
				Environment.SetEnvironmentVariable("NO_COLOR", "1", EnvironmentVariableTarget.Process);
				string formatArg = "";
				if (!noIife)
					formatArg = " -f iife";
				await ExecAsync(
					"rollup \"" + srcFileName + "\" -o \"" + bundleFileName + "\"" + formatArg + " -m -p rollup-plugin-sourcemaps" + banner,
					fileDir,
					utf8: true);

				if ((iifeParams != "" || iifeArgs != "") &&
					File.Exists(Path.Combine(fileDir, bundleFileName)))
				{
					string[] lines = File.ReadAllLines(Path.Combine(fileDir, bundleFileName));
					for (int i = 0; i < 3; i++)
					{
						if (Regex.IsMatch(lines[i], @"^\(function \(\) \{\s*$"))
						{
							lines[i] = lines[i].Replace("()", "(" + iifeParams + ")");
							break;
						}
					}
					for (int i = lines.Length - 1; i > lines.Length - 4; i--)
					{
						if (Regex.IsMatch(lines[i], @"^\}\(\)\);\s*$") ||
							Regex.IsMatch(lines[i], @"^\}\)\(\);\s*$"))
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
			if (needsRecompile) return;   // Abort this run and restart

			if (Status != false)
			{
				string mapParam = "--source-map \"url='" + onlyMinFileName.Replace('\\', '/') + ".map'\"";
				if (File.Exists(Path.Combine(fileDir, bundleFileName) + ".map"))
				{
					// The url part must refer to the file without the build directory because
					// both files (min.js and map) will be in the same directory.
					mapParam = "--source-map \"content='" + bundleFileName.Replace('\\', '/') + ".map',url='" + onlyMinFileName.Replace('\\', '/') + ".map'\"";
				}
				await ExecAsync(
					"uglifyjs " + bundleFileName + " --compress --mangle --output \"" + minFileName + "\" --comments \"/^!/\" " + mapParam,
					fileDir,
					utf8: true);

				PostprocessMapFile(minFileName + ".map");
			}
			if (needsRecompile) return;   // Abort this run and restart

			if (Status != false)
			{
				RestoreResultFileTime(minFileName);
				RestoreResultFileTime(minFileName + ".map");

				if (!Project.KeepIntermediaryFiles && !Project.KeepUnminifiedFiles)
				{
					if (bundleFileName != srcFileName)
					{
						File.Delete(Path.Combine(fileDir, bundleFileName));
						File.Delete(Path.Combine(fileDir, bundleFileName + ".map"));
						bundleFileName = "";
					}
				}

				LastLog += Environment.NewLine + "Compiled files:" + Environment.NewLine;
				if (!string.IsNullOrEmpty(bundleFileName))
					LastLog += "- " + bundleFileName + Environment.NewLine;
				LastLog += "- " + minFileName + Environment.NewLine;

				if (isAnyFileModified)
				{
					LastCompileTime = DateTime.UtcNow;
				}
				else
				{
					LastLog += "Note: File content has not changed to previous build." + Environment.NewLine;
				}

				SetCompressedResultSize(Path.Combine(fileDir, minFileName));
			}
		}

		private async Task CompileScss(bool force)
		{
			string scssFileName = Path.GetFileName(fullFileName);
			string cssFileName = Path.GetFileNameWithoutExtension(fullFileName) + ".css";
			string minCssFileName = Path.GetFileNameWithoutExtension(fullFileName) + ".min.css";

			string buildDir = "";
			using (var reader = new StreamReader(fullFileName))
			{
				int lineNumber = 0;
				while (!reader.EndOfStream && lineNumber < 10)
				{
					string line = reader.ReadLine();
					lineNumber++;
					var match = Regex.Match(line, @"^\s*/\*\s*build-dir\((.*)\)\s*\*/", RegexOptions.IgnoreCase);
					if (match.Success && buildDir == "")
					{
						buildDir = match.Groups[1].Value.Trim().Replace('/', '\\');
						if (buildDir != "" && !buildDir.EndsWith("\\"))
							buildDir += "\\";
						if (buildDir != "")
						{
							cssFileName = Path.Combine(buildDir, cssFileName);
							minCssFileName = Path.Combine(buildDir, minCssFileName);

							Directory.CreateDirectory(Path.Combine(fileDir, buildDir));
						}
					}
				}
			}

			if (!force && AreFilesUpToDate(minCssFileName, minCssFileName + ".map"))
			{
				SetCompressedResultSize(Path.Combine(fileDir, minCssFileName));
				return;
			}
			if (!force) Status = null;

			ClearFileTimes();
			SaveResultFileTime(minCssFileName);
			SaveResultFileTime(minCssFileName + ".map");
			LastLog = "";

			await ExecAsync(
				"sass \"" + scssFileName + "\" \"" + cssFileName + "\"",
				fileDir,
				utf8: true);
			if (needsRecompile) return;   // Abort this run and restart

			if (Status != false)
			{
				await ExecAsync(
					"csso \"" + cssFileName + "\" --output \"" + minCssFileName + "\" --source-map \"" + minCssFileName + ".map\"",
					fileDir);
				PostprocessMapFile(minCssFileName + ".map");
			}
			if (needsRecompile) return;   // Abort this run and restart

			if (Status != false)
			{
				RestoreResultFileTime(minCssFileName);
				RestoreResultFileTime(minCssFileName + ".map");

				if (!Project.KeepIntermediaryFiles && !Project.KeepUnminifiedFiles)
				{
					File.Delete(Path.Combine(fileDir, cssFileName));
					File.Delete(Path.Combine(fileDir, cssFileName + ".map"));
					cssFileName = "";
				}

				LastLog += Environment.NewLine + "Compiled files:" + Environment.NewLine;
				if (!string.IsNullOrEmpty(cssFileName))
					LastLog += "- " + cssFileName + Environment.NewLine;
				LastLog += "- " + minCssFileName + Environment.NewLine;

				if (isAnyFileModified)
				{
					LastCompileTime = DateTime.UtcNow;
				}
				else
				{
					LastLog += "Note: File content has not changed to previous build." + Environment.NewLine;
				}

				SetCompressedResultSize(Path.Combine(fileDir, minCssFileName));
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
			var jsonParams = new JSONParameters
			{
				UsingGlobalTypes = false,
				UseExtensions = false
			};
			json = JSON.ToJSON(dict, jsonParams);
			File.WriteAllText(fileName, json);
		}

		#endregion Compiling

		#region Process helper methods

		private bool AreFilesUpToDate(params string[] outputs)
		{
			string source = Path.Combine(Project.ProjectPath, FilePath);
			string path = Path.GetDirectoryName(source);
			DateTime latestSourceTime = GetLastWriteTimeUtcSafe(source);
			foreach (string addFile in AdditionalSourceFiles)
			{
				DateTime addFileTime = GetLastWriteTimeUtcSafe(Path.Combine(Project.ProjectPath, addFile));
				if (addFileTime > latestSourceTime)
					latestSourceTime = addFileTime;
			}

			DateTime latestOutputTime = DateTime.MinValue;
			bool allUpToDate = true;
			foreach (string output in outputs)
			{
				DateTime outputTime = GetLastWriteTimeUtcSafe(Path.Combine(path, output));
				if (outputTime > latestOutputTime)
				{
					latestOutputTime = outputTime;
				}
				if (!File.Exists(Path.Combine(path, output)) || outputTime < latestSourceTime)
				{
					Debug.WriteLine("File not up-to-date: " + FilePath);
					allUpToDate = false;
				}
			}
			if (LastCompileTime == DateTime.MinValue && latestOutputTime != DateTime.MinValue)
			{
				LastCompileTime = latestOutputTime;
			}
			return allUpToDate;
		}

		private static DateTime GetLastWriteTimeUtcSafe(string path)
		{
			if (File.Exists(path))
				return File.GetLastWriteTimeUtc(path);
			return DateTime.MinValue;
		}

		private async Task ExecAsync(string cmdline, string directory, bool utf8 = false)
		{
			// Try to find the command executable file in the application directory, then use that
			string[] parts = cmdline.Split(new[] { ' ' }, 2);
			if (File.Exists(Path.Combine(App.AppDir, parts[0] + ".exe")) ||
				File.Exists(Path.Combine(App.AppDir, parts[0] + ".cmd")))
			{
				cmdline = "\"\"" + App.AppDir + Path.DirectorySeparatorChar + parts[0] + "\" " + parts[1] + "\"";
			}
			if (!string.IsNullOrEmpty(LastLog))
				LastLog += Environment.NewLine;
			LastLog += "► " + cmdline + Environment.NewLine;

			var psi = new ProcessStartInfo("cmd", "/c " + cmdline)
			{
				CreateNoWindow = true,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				UseShellExecute = false,
				WorkingDirectory = directory
			};
			if (utf8)
			{
				psi.StandardOutputEncoding = System.Text.Encoding.UTF8;
				psi.StandardErrorEncoding = System.Text.Encoding.UTF8;
			}

			var process = Process.Start(psi);
			await Task.Run(() => process.WaitForExit(10000));
			string stdErr = process.StandardError.ReadToEnd();
			string stdOut = process.StandardOutput.ReadToEnd();
			if (!string.IsNullOrWhiteSpace(stdErr))
			{
				// Remove complete whitespace lines at beginning and any whitespace at end
				stdErr = Regex.Replace(stdErr, @"^( *[\r\n])*|\s$", "");
				LastLog += stdErr + Environment.NewLine;
			}
			if (!string.IsNullOrWhiteSpace(stdOut))
			{
				// Remove complete whitespace lines at beginning and any whitespace at end
				stdOut = Regex.Replace(stdOut, @"^( *[\r\n])*|\s$", "");
				LastLog += stdOut + Environment.NewLine;
			}
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

		private void SetCompressedResultSize(string fileName)
		{
			try
			{
				using (var ms = new MemoryStream())
				{
					using (var fs = File.OpenRead(fileName))
					using (var gs = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
					{
						fs.CopyTo(gs);
					}
					CompressedResultSize = ms.Length;
				}
			}
			catch (IOException)
			{
				CompressedResultSize = -1;
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
					var match = Regex.Match(line, @"^\s*import\s+.*?([""'])(.+?)\1");
					if (match.Success)
					{
						string file = match.Groups[2].Value.Trim();
						if (Path.GetExtension(file) != ".js")
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
					var match = Regex.Match(line, @"^\s*@(?:forward|import|use)\s+(?:(""|')(.+?)\1\s*,?\s*)+");
					if (match.Success)
					{
						foreach (Capture capture in match.Groups[2].Captures)
						{
							string file = capture.Value.Trim();
							file = file.Replace("/", "\\");
							file = Path.Combine(filePath, file);

							switch (Path.GetExtension(file))
							{
								case ".css":
								case ".scss":
								case ".sass":
									if (AdditionalSourceFiles.Add(file))
										DetectAdditionalSourceFilesScss(file, clear: false);
									break;
								default:
									// Add all possible variants as they may exist or change later
									if (AdditionalSourceFiles.Add(file + ".css"))
										DetectAdditionalSourceFilesScss(file + ".css", clear: false);
									if (AdditionalSourceFiles.Add(file + ".scss"))
										DetectAdditionalSourceFilesScss(file + ".scss", clear: false);
									if (AdditionalSourceFiles.Add(file + ".sass"))
										DetectAdditionalSourceFilesScss(file + ".sass", clear: false);

									string partialFile = Path.Combine(Path.GetDirectoryName(file), "_" + Path.GetFileName(file));
									if (AdditionalSourceFiles.Add(partialFile + ".css"))
										DetectAdditionalSourceFilesScss(partialFile + ".css", clear: false);
									if (AdditionalSourceFiles.Add(partialFile + ".scss"))
										DetectAdditionalSourceFilesScss(partialFile + ".scss", clear: false);
									if (AdditionalSourceFiles.Add(partialFile + ".sass"))
										DetectAdditionalSourceFilesScss(partialFile + ".sass", clear: false);
									break;
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
				var info = new FileTimeInfo
				{
					LastWriteTimeUtc = File.GetLastWriteTimeUtc(Path.Combine(fileDir, fileName))
				};
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
					// It's a bad idea to revert the write time of unchanged files because this will
					// recompile these files at each app startup, and again not update the timestamp.
					// Without this, the output file timestamp is always updated at compilation
					// (even if unchanged) so that it will keep still at the next app start.
					//File.SetLastWriteTimeUtc(Path.Combine(fileDir, fileName), info.LastWriteTimeUtc);
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
