Building Documentation
======================

The following prerequisites are required to build the documentation for the
library.

 * Microsoft Sandcastle 2.6
   
   A documentation compiler for .NET assemblies.
   Download from <http://sandcastle.codeplex.com/releases>.
 
 * Sandcastle Help File Builder 1.9.1
   
   A user interface and project management system for Sandcastle documentation.
   Download from <http://shfb.codeplex.com/releases>.

Once these tools are installed, open the appropiate .shfbproj file in the
Documentation directory and run build. This action generates the user-readable
documentation for the library (Release build by default).

Output
======

Upon a successful build, the 'output' directory is created, containing the
following relevant files.

 * IRC.NET.chm
   
   The Microsoft Compiled HTML Help file containing the full documentation.
   This file can be viewed using the Microsoft HTML Help v1 viewer, included
   with all recent versions of Windows.
 
 * IRC.NET.mshc
   
   The Microsoft Help Container file containing the full documentation.
   This file provides content to be viewed in Microsoft Help Viewer 1.0,
   included with Visual Studio 2010.
   
   To install the help content run the Install_IRC.NET.bat script.
   To remove the help content run the Remove_IRC.NET.bat script.
   
   See <http://msdn.microsoft.com/en-us/library/dd776252.aspx> for more
   information on Microsoft Help Viewer 1.0.
