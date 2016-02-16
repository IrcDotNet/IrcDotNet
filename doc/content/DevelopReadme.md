# IRC.NET Development

## Building with Visual Studio

First, [Install ASP.Net 5](http://docs.asp.net/en/latest/getting-started/installing-on-windows.html) to get the DNX tools integration for Visual Studio.

Open the solution (`.sln`) file in Visual Studio 2015. You should now be able to build normally.

## Building with DNX CLI

This build option requires a Linux shell. On Windows you can use a MingW shell. The easiest, somewhat cheating method is to use the Git Bash as a general purpose bash shell for Windows.

If you're on windows, [Install Git](http://git-scm.com/) and make sure to enable the "Git Bash" option as well as the "Add to context menu" option.

To install DNX on Linux or in a bash shell on Windows, run the following commands:

 - `curl -sSL https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.sh | DNX_BRANCH=dev sh && source ~/.dnx/dnvm/dnvm.sh`
 - `dnvm upgrade`

This will download and upgrade the dotnet version manager and CoreCLR (or Mono for Linux) execution environments.

Next, compile IrcDotNet:

- `cd Path/To/IrcDotNet`
- `dnu restore`
- `dnu build source/IrcDotNet/`

To run the tests:

- `dnu build test/IrcDotNet.Test/`
- `cd test/IrcDotNet.Test && dnx IrcDotNet.Test`

## Overview

This project aims to be a simple, flexible, and efficient implementation of the IRC protocol in C#, for the .NET platform.

### Issues & Features

New features are accepted via GitHub pull requests from the [official repository](https://github.com/alexreg/ircdotnet).

Issues and TODOs are tracked on [GitHub](https://github.com/alexreg/ircdotnet/issues).

General discussions is held in our [IRC channel](irc://chat.freenode.net/).

### Versioning

This project uses [Semantic Versioning](http://semver.org/).

### Documentation

- `IrcDotNet`: the core of the IRC.NET implementation; basically, all you need to get started.

### Project Structure

- /doc/

    Project documentation files. This folder contains both development and user documentation.


- /source/

		The root for all projects (not including unit test projects).

- /samples/

		The root for all sample projects.

- /test/

		The root for all unit test projects.


Each project should have has a corresponding project with the name `${ProjectName}.Test` in the test folder.
This test project provides unit tests for the project `${ProjectName}`.

## Building Documentation

To build documentation, from within the `doc/build` directory:

 - `./build.sh LocalDoc`
