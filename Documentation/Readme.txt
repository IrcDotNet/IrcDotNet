Building Documentation
======================

The following prerequisites are required to build the documentation for the
library.

 * mdoc
   
   This is included in the standard Mono installation or can be downloaded
   separately from <http://www.mono-project.com/Mdoc>. Building has been tested
   using the version included in the Mono 2.6.1 package.
 
 * PowerShell 2.0
   
   This is included by default with all Windows 7 installations, but may be
   downloaded and installed for previous versions of Windows as part of the
   Windows Management Framework, available from
   <http://support.microsoft.com/kb/968929>.

Once these tools are installed, the Build.ps1 file may need to be edited. The
mdocPath variable gives the path to the mdoc executable, and should be set to
the appropiate path (the Mono bin directory if using the version installed with
Mono).

Finally, the documentation can be built by running the Build.ps1 script with
PowerShell. The script uses the mdoc tool to generate the documentation in
various formats, as described in the Build Output section.

Build Output
============

Upon a successful build, the 'output' directory is created, containing the
following sub-directories.

 * mdoc
   
   Contains the documentation in the Mono documentation (mdoc) XML format. The
   format is specified at
   <http://www.go-mono.org/docs/index.aspx?link=man:mdoc%285%29>.
 
 * msxdoc
   
   Contains the documentation in the Microsoft XML documentation format. The
   format is specified at
   <http://msdn.microsoft.com/en-us/library/b2s063f7.aspx>.
 
 * html
   
   Contains the documentation in HTML format. This is the primary human-readable
   form of documentation.
