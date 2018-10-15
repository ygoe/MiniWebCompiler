using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

[assembly: AssemblyProduct("Mini Web Compiler")]
[assembly: AssemblyTitle("MiniWebCompiler")]
[assembly: AssemblyDescription("Compiles files for web sites")]
[assembly: AssemblyCopyright("© {copyright:2018-} Yves Goergen")]
[assembly: AssemblyCompany("unclassified software development")]

[assembly: AssemblyVersion("0.0")]
[assembly: AssemblyFileVersion("0.0")]
[assembly: AssemblyInformationalVersion("{semvertag}")]

// Indicate the build configuration
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

// Other attributes
[assembly: ComVisible(false)]
[assembly: ThemeInfo(
	// Where theme specific resource dictionaries are located
	// (used if a resource is not found in the page, or application resource dictionaries)
	ResourceDictionaryLocation.SourceAssembly,
	// Where the generic resource dictionary is located
	// (used if a resource is not found in the page, app, or any theme specific resource dictionaries)
	ResourceDictionaryLocation.SourceAssembly
)]
