# IRC.Net implementation documentation 

## Building

Open the ``.sln`` file or run ``./build``.

> NOTE: It is possible that you can only build the ``.sln`` file AFTER doing an initial ``./build`` (because nuget dependencies have to be resolved).

## General overview:

This project aims to be a very flexible, extendable and good performing IRC implementation in C#.

### Issues / Features / TODOs

New features are accepted via github pull requests (so just fork away right now!):  https://github.com/alexreg/ircdotnet.

Issues and TODOs are tracked on github, see: https://github.com/alexreg/ircdotnet/issues.

Discussions/Forums are on IRC. 

### Versioning: 

http://semver.org/

### High level documentation ordered by project.

- `IrcDotNet`: The Core of the IRC.Net implementation, basically all you need to get started.

### The Project structure:

- /.nuget/

	Nuget dependencies will be downloaded into this folder. 
	This folder can safely be deleted without affecting the build.

- /build/

	The project assemblies will be build into this folder. This folder can safely be deleted without affecting the build.

- /lib/

	library dependencies (currently not used). Most dependencies are automatically managed by nuget and not in this folder. 
	Only some internal dependencies and packages not in nuget. The git repository should always be "complete".

- /doc/

	Project documentation files. This folder contains both development and user documentation.

- /src/

	The Solution directory for all projects

	- /src/source/

		The root for all projects (not including unit test projects).

	- /src/samples/

		The root for all sample projects.
    
	- /src/test/

		The root for all unit test projects.

- /test/

	The unit test assemblies will be build into this folder. This folder can safely be deleted without affecting the build.

- /tmp/

	This folder is ignored by git.

- /build.cmd, /build.sh, /build.fsx

	Files to directly start a build including unit tests via console (windows & linux).

-  /packages.config

	Nuget packages required by the build process.


Each project should have has a corresponding project with the name `Test.${ProjectName}` in the test folder.
This test project provides unit tests for the project `${ProjectName}`.

## Advanced Building

The build is done in different steps and you can execute the build until a given step or a single step:

First `build.sh` and `build.cmd` restore build dependencies and `nuget.exe`, then build.fsx is invoked:

 - `Clean`: cleans the directories (previous builds)
 - `RestorePackages`: restores nuget packages
 - `SetVersions`: sets the current version
 - `BuildApp_40`: build for net40
 - `BuildTest_40`: build the tests for net40
 - `Test_40`: run the tests for net40
 - `BuildApp_45`: build for net45
 - `BuildTest_45`: build the tests for net45
 - `Test_45`: run the tests for net45
 - `CopyToRelease`: copy the generated .dlls to release/lib
 - `LocalDoc`: create the local documentation you can view that locally
 - `All`: this does nothing itself but is used as a marker (executed by default when no parameter is given to ./build)
 - `VersionBump`: commits all current changes (when you change the version before you start the build you will have some files changed)
 - `NuGet`: generates the nuget packages
 - `GithubDoc`: generates the documentation for github
 - `ReleaseGithubDoc`: pushes the documentation to github
 - `Release`: a marker like "All"

You can execute all steps until a given point with `./build #Step#` (replace #Step# with `Test_40` to execute `Clean`, `RestorePackages`, `SetVersions`, ..., `Test_40`)

You can execute a single step with `build #Step#_single`: For example to build the nuget packages you can just invoke `./build NuGet_single` 

> Of course you need to have the appropriate dlls in place (otherwise the Nuget package creation will fail); ie have build IRC.Net before.


There is another (hidden) step `CleanAll` which will clean everything up (even build dependencies and the downloaded Nuget.exe), 
this step is only needed when build dependencies change. `git clean -d -x -f` is also a good way to do that

## Visual Studio / Monodevelop

As mentioned above you need to `build` at least once before you can open the 
solution file (`src/IrcDotNet.sln`) with Visual Studio / Monodevelop.

The default is that Visual Studio is configured for the latest build (`net45`).
If you want to build another target with Visual Studio / Monodevelop do the following:

 - Close the solution
 - Open `src/buildConfig.targets` and change the `CustomBuildName` entry (near the top) to `sl40`, `net45` or `net40`.
   The line should then look like this:
   
   ```markup
   <CustomBuildName Condition=" '$(CustomBuildName)' == '' ">net40</CustomBuildName> 
   ```

 - Save the `src/buildConfig.targets` file and re-open the solution.
