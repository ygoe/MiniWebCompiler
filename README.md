Mini Web Compiler
=================

Compiles JavaScript code and SCSS stylesheets for the web, in a simple and clean GUI for Windows.

This application manages project files stored in JSON format. Each project you manage with Mini Web Compiler needs a separate miniwebcompiler.json file in its root directory. It specifies the project’s name, a few options and most importantly the source files to compile. You can create or add such project files in the Mini Web Compiler application and they are active until you remove them. Once the tool detects that one of these files (or the files they reference) has changed, it starts compiling the file according to its type and the project options.

The following file types are supported:

* CSS: Minify
* JavaScript: Bundle, transpile to ECMAScript 5 (for Internet Explorer), minify
* SCSS: Compile to CSS, minify

Most of the actual processing and compilation is done by widely recognised and established tools. Most of them are made available as NPM packages that run on Node.js. Others need to be compiled from source code to a native binary. Some of the tools were patched to ensure proper handling of source maps (which are fully maintained along all the way) and important source comments.

These are the tools that do the work:

* [Babel](https://github.com/babel/babel): Transpile modern JavaScript to ECMAScript 5
* [CSSO](https://github.com/css/csso): Minify CSS
* [rollup.js](https://github.com/rollup/rollup): Bundle JavaScript modules
* [Sass](https://sass-lang.com/install): Compile SASS and SCSS to CSS
* [UglifyJS](https://github.com/mishoo/UglifyJS): Minify JavaScript

All these tools are included in the setup package. The actual versions can be seen in the application’s About dialog.

**Warning:** Babel in Mini Web Compiler is deprecated. It requires a patch to support source maps but that patch can no longer be applied. A newer version of Babel (v7 instead of v6) is available but not included in Mini Web Compiler. Its only use is to convert modern JavaScript for Internet Explorer, which itself is deprecated and should no longer be used or supported. Current browsers all support modern JavaScript so this time-consuming extra step is no longer necessary.

JavaScript configuration
------------------------
JavaScript files are run through `rollup` to bundle them into a single file, if any imports of other files are detected in the source file. The bundled code is wrapped into an immediately invoked function expression (IIFE). The parameters of that function and the arguments when calling it can be specified with comment lines like `/* iife-params($) */` and `/* iife-args(jQuery) */` near the top of the file. The code between the parentheses is inserted into the generated file.

JavaScript files are transpiled to ECMAScript 5 if they contain the comment line `/* ecmascript */` near the top of the file. (This is **deprecated**, see the warning above.)

System requirements
-------------------
* Windows 7, 8, 10 (64-bit)
* .NET Framework 4.7.2 or later (comes with Windows Update)
* No Node.js (included in the setup package)
* No other tools (included in the setup package)

Build prerequisites
-------------------
These steps must be taken to use the application from source code and build the setup package.

**Note:** None of this applies when installing Mini Web Compiler from the setup package.

* Install Node.js for Windows from https://nodejs.org
* Install babel-cli: `npm install babel-cli -g` (This is the outdated v6, see warning above.)
* Patch babel-cli as in https://github.com/babel/babel/issues/3940#issuecomment-365911189
* Install babel-preset-env: `npm install babel-preset-env -g`
* Install babel-preset-minify: `npm install babel-preset-minify -g`
* Install csso: `npm install csso-cli -g`
* Install rollup: `npm install rollup -g`
* Install uglify-es: `npm install uglify-js -g`
* Install sass: `npm install sass -g`

License
-------
[MIT license](https://github.com/ygoe/MiniWebCompiler/blob/master/LICENSE)

See LICENSE-3RD-PARTY.txt for licenses of third-party tools that are included in the setup package.
