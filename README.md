Mini Web Compiler
=================

Compiles JavaScript code and SCSS stylesheets for the web, in a simple and clean GUI for Windows.

This application manages project files stored in JSON format. Each project you manage with Mini Web Compiler needs a separate miniwebcompiler.json file in its root directory. It specifies the project’s name, a few options and most importantly the source files to compile. You can create or add such project files in the Mini Web Compiler application and they are active until you remove them. The application runs in the background and once it detects that one of these files (or the files they reference) has changed, it starts compiling the file according to its type and the project options.

The following file types are supported:

* CSS: Minify
* JavaScript: Bundle, transpile to ECMAScript 5 (for Internet Explorer), minify
* SCSS: Compile to CSS, minify

Most of the actual processing and compilation is done by widely recognised and established tools. All of them are made available as NPM packages that run on Node.js. They are bundled together in a managing GUI that calls the tools with the appropriate parameters and takes care of the necessary file handling to ensure proper source maps (which are fully maintained along all the way) and keep important source comments like license summaries.

These are the tools that do the work:

* [Babel](https://github.com/babel/babel): Transpile modern JavaScript to ECMAScript 5 (deprecated, see warning below)
* [CSSO](https://github.com/css/csso): Minify CSS
* [rollup.js](https://github.com/rollup/rollup): Bundle JavaScript modules
* [rollup-plugin-sourcemaps](https://github.com/maxdavidson/rollup-plugin-sourcemaps): Rollup plugin to read input source maps (for bundling bundles)
* [Sass](https://github.com/sass/dart-sass): Compile SASS and SCSS to CSS
* [UglifyJS](https://github.com/mishoo/UglifyJS): Minify JavaScript

All these tools are included in the setup package. They are installed locally in the application directory so they won’t interfer with any globally installed NPM packages/tools. The actual versions can be seen in the application’s About dialog.

**Warning:** Babel in Mini Web Compiler is deprecated. It requires a patch to support source maps but that patch can no longer be applied. A newer version of Babel (v7 instead of v6) is available but not included in Mini Web Compiler. Its only use is to convert modern JavaScript for Internet Explorer, which itself is deprecated and should no longer be used or supported. Current browsers all support modern JavaScript so this time-consuming extra step is no longer necessary.

JavaScript configuration
------------------------
JavaScript files are run through `rollup` to bundle them into a single file, if any imports of other files are detected in the source file. The bundled code is wrapped into an immediately invoked function expression (IIFE). The parameters of that function and the arguments when calling it can be specified with comment lines like `/* iife-params($) */` and `/* iife-args(jQuery) */` near the top of the file. The code between the parentheses is inserted into the generated file. The comment `/* no-iife */` disables the use of an IIFE and basically concatenates all the files together.

The build output files can be written to a separate directory to keep your source code folder tidy. Use a comment like `/* build-dir(...) */` and anything in the parentheses is the relative path to your build output files. The build directory is created if it doesn’t exist. This configuration also works for CSS files, but note that you cannot use relative `url()` references without manually copying the build file to the correct directory.

JavaScript files are transpiled to ECMAScript 5 if they contain the comment line `/* ecmascript */` near the top of the file. (This is **deprecated** and will be removed in a future version, see the warning above.)

### Example

This is how it looks like in a JavaScript source files:

```js
/*! frontfire-core.js v2.0.0-dev | @license MIT | ygoe.de */
/* build-dir(build) */

// Copyright (c) 2022, Yves Goergen, https://ygoe.de
// (More of the license text)

"Source code here...";
```

And here is a bundling example that merges several JavaScript files into a single output file:

```js
/*! frontfire-core-singlefile.js v2.0.0-dev | @license MIT | ygoe.de */
/* no-iife */
/* build-dir(build) */

// Copyright (c) 2022, Yves Goergen, https://ygoe.de
// (More of the license text)

import "./arraylist";
import "./frontfire-core";
import "./color";
import "./datacolor";
```

Script automation
-----------------
If the file miniwebcompiler.cmd exists in the project directory (next to miniwebcompiler.json), then it will be called after each file was compiled (except if unchanged, but still when clicking the *Compile all* button). It will be passed the name of the compiled source file (as listed in the project with its relative path) as first argument.

This script can be used to perform additional steps after a compiled file was updated, like post-processing with external tools or copying it into other project directories conveniently.

### Example

Here’s what I do with these scripts:

```cmd
@echo off
if "%~1" == "ui\src\css\frontfire-ui-complete.scss" (
	echo Copying frontfire-ui-complete.min.css to my website
	copy /y ui\src\css\build\frontfire-ui-complete.min.css* C:\Web\mywebsite\lib\frontfire2 >nul
	echo Copying frontfire-ui-complete.min.css to Farbeimer
	copy /y ui\src\css\build\frontfire-ui-complete.min.css* C:\Web\farbeimer\lib\frontfire2 >nul
)
if "%~1" == "ui\src\js\frontfire-ui-complete-singlefile.js" (
	echo Copying frontfire-ui-complete-singlefile.min.js to my website
	copy /y ui\src\js\build\frontfire-ui-complete-singlefile.min.js* C:\Web\mywebsite\lib\frontfire2 >nul
	echo Copying frontfire-ui-complete-singlefile.min.js to Farbeimer
	copy /y ui\src\js\build\frontfire-ui-complete-singlefile.min.js* C:\Web\farbeimer\lib\frontfire2 >nul
)
```

System requirements
-------------------
* Windows 7, 8, 10, 11 (64-bit) (currently only tested in Windows 10)
* .NET Framework 4.7.2 or later (comes with Windows Update)
* No Node.js (included in the setup package)
* No other tools (included in the setup package)

Download
--------
Please go to the releases section of this repository to find the latest installation package. It extracts all files into a directory in your user profile (no administrator privileges used) and sets up the start menu and autostart shortcuts.

Build prerequisites
-------------------
These steps must be taken to use the application from source code and build the setup package. The setup will pick up the files from their global installation path. This means that globally installing the npm packages is required on the build machine, but the installed application will always use its own embedded versions of the tools.

**Note:** None of this applies when installing Mini Web Compiler from the setup package.

* Install Node.js for Windows from https://nodejs.org
* (*) Install babel-cli: `npm install babel-cli -g` (This is the outdated v6, see warning above.)
* (*) Patch babel-cli as in https://github.com/babel/babel/issues/3940#issuecomment-365911189
* (*) Install babel-preset-env: `npm install babel-preset-env -g`
* (*) Install babel-preset-minify: `npm install babel-preset-minify -g`
* Install csso: `npm install csso-cli -g` (See note below)
* Install rollup: `npm install rollup -g`
* Install rollup-plugin-sourcemaps: `npm install rollup-plugin-sourcemaps -g`
* Install uglify-es: `npm install uglify-js -g`
* Install sass: `npm install sass -g`

The steps marked with (*) are necessary for the deprecated transpiling support. It should be okay to leave them away and just fix any errors related to missing files.

To upgrade all tools to the latest version, run: `npm update -g`

After upgrading the tools, all patches mentioned above must be applied again.

### Latest csso version

The csso-cli package isn’t up-to-date with the current csso library. It contains bugs that have been resolved in csso 5.0. To use the latest version, don’t install the public package as described above, but follow these steps instead:

```
git clone https://github.com/ygoe/csso-cli.git
cd csso-cli
git checkout patch-1
npm pack
npm install -g csso-cli-5.0.0.tgz
```

These steps become obsolete when csso-cli has been updated (see [PR](https://github.com/css/csso-cli/pull/26)).

License
-------
[MIT license](https://github.com/ygoe/MiniWebCompiler/blob/master/LICENSE)

See Setup/LICENSE-3RD-PARTY.txt for licenses of third-party tools that are included in the setup package.
