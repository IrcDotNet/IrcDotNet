Building
========

The following prerequisites must be installed in order to build the
documentation for the library.

 * *Microsoft Sandcastle* 2.6
   
   A documentation compiler for .NET assemblies.
   Download from <http://sandcastle.codeplex.com/releases>.
 
 * *Sandcastle Help File Builder* 1.9.x
   
   A user interface and project management system for Sandcastle documentation.
   Download from <http://shfb.codeplex.com/releases>.

Once these tools are installed, open the appropiate `.shfbproj` file in the
directory and run the build. This action generates the entire user-readable
documentation for the library (in *Release* configuration by default).

Output
======

Upon a successful build, the `out` directory is created, containing the
following relevant files.

 * `IrcDotNet.chm`
   
   The Microsoft Compiled HTML Help file containing the full documentation.
   This file can be viewed using the Microsoft HTML Help v1 viewer, included
   with all recent versions of Windows.
 
 * `IrcDotNet.mshc`
   
   The Microsoft Help Container file containing the full documentation.
   This file provides content to be viewed in Microsoft Help Viewer 1.x,
   which is included with Visual Studio 2010.
   
   To install the help content run the `Install_IrcDotNet.bat` script.
   To remove the help content run the `Remove_IrcDotNet.bat` script.
   
   See <http://msdn.microsoft.com/en-us/library/dd776252.aspx> for more
   information on Microsoft Help System 1.x.
