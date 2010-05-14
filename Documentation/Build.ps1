# Build parameters
$mdocPath = "C:/mono/bin"
$releasePath = "../IrcDotNet/bin/Release/"

$Error.Clear()
$ErrorActionPreference = "Stop"

Write-Output "Building documentation..."

# Add mdoc bin directory to path.
$env:Path = "${mdocPath};" + $env:Path

# Generate mdoc files from MS XML doc files.
Write-Output "Generating mdoc files..."
& mdoc update "-i=../IrcDotNet/bin/Release/IrcDotNet.xml" `
	"-L=../IrcDotNet/bin/Release/" "-o=output/mdoc/en/" `
	../IrcDotNet/bin/Release/IrcDotNet.dll
Write-Output "Done."

# Export mdoc files back as MS XML doc files.
Write-Output "Generating MS XML doc files..."
Remove-Item "output/msxdoc" -Recurse -ErrorAction SilentlyContinue | out-null
New-Item "output/msxdoc/en/" -ItemType directory | out-null
& mdoc export-msxdoc "-o=output/msxdoc/en/IrcDotNet.xml" output/mdoc/en/
Write-Output "Done."

# Export mdoc files as HTML files.
Write-Output "Generating HTML files..."
& mdoc export-html "-o=output/html/en/" output/mdoc/en/
Write-Output "Done."

# Check if build was successful.
if (!$Error.Count)
{
    Write-Output "Documentation built successfully."
}
