Overiview
=========

This directory contains tools for building NuGet <http://nuget.org/> packages
for the binaries of the library.

Building
========

The following prerequisites must be installed in order to build the NuGet
packages for the library.

 * *NuGet Command Line* 1.4
   A command-line program for creating and managing NuGet packages.
   The `NuGet.exe` executable must be in the PATH environment variable.
   Download from <http://nuget.codeplex.com/releases/view/58939>.

Once these tools are installed, and the library has been built in Release mode,
the NuGet packages may be built by running the relevant commands from this
directory.

 * Building the library package:
   
   > nuget pack IrcDotNet.nuspec -OutputDirectory out/
 
 * Building the samples package:
   
   > nuget pack IrcDotNet.Sample.nuspec -Exclude **\obj\** -OutputDirectory out/

Output
======

Upon a successful build, the `out` directory is created, containing the built
NuGet packages (`.nupkg` files).
