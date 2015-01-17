// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

(*
    This file handles the complete build process of RazorEngine

    The first step is handled in build.sh and build.cmd by bootstrapping a NuGet.exe and 
    executing NuGet to resolve all build dependencies (dependencies required for the build to work, for example FAKE)

    The secound step is executing this file which resolves all dependencies, builds the solution and executes all unit tests
*)


// Supended until FAKE supports custom mono parameters
#I @".nuget/Build/FAKE/tools/" // FAKE
#r @"FakeLib.dll"  //FAKE

open System.Collections.Generic
open System.IO

open Fake
open Fake.Git
open Fake.FSharpFormatting
open AssemblyInfoFile

// properties
let projectName = "IRC.Net"
let copyrightNotice = "IRC.Net Copyright © Alex Regueiro 2011-2015 @"
let projectSummary = "IRC.NET is a complete IRC (Internet Relay Chat) client library for the .NET Framework."
let projectDescription = "IRC.NET aims to provide a complete and efficient implementation of the protocol as described in RFCs 1459 and 2812, as well as de-facto modern features of the protocol."
let authors = ["Alex Regueiro"]
let page_author = "Matthias Dittrich"
let mail = "alexreg@me.com"
let version = "0.4.1.0"
let version_nuget = "0.4.1"
let commitHash = Information.getCurrentSHA1(".")

//let buildTargets = environVarOrDefault "BUILDTARGETS" ""
//let buildPlatform = environVarOrDefault "BUILDPLATFORM" "MONO"
let buildDir = "./build/"
let releaseDir = "./release/"
let outLibDir = "./release/lib/"
let outDocDir = "./release/documentation/"
let docTemplatesDir = "./doc/templates/"
let testDir  = "./test/"
let nugetDir  = "./.nuget/"
let packageDir  = "./.nuget/packages"

let github_user = "alexreg"
let github_project = "ircdotnet"
let nuget_url = "https://www.nuget.org/packages/IrcDotNet/"

let tags = "communication networking irc ctcp"

let buildMode = "Release" // if isMono then "Release" else "Debug"

// Where to look for *.cshtml templates (in this order)
let layoutRoots =
    [ docTemplatesDir; 
      docTemplatesDir @@ "reference" ]

if isMono then
    monoArguments <- "--runtime=v4.0 --debug"
    //monoArguments <- "--runtime=v4.0"

let github_url = sprintf "https://github.com/%s/%s" github_user github_project
    
// Ensure the ./src/.nuget/NuGet.exe file exists (required by xbuild)
let nuget = findToolInSubPath "NuGet.exe" "./.nuget/Build/NuGet.CommandLine/tools/NuGet.exe"
System.IO.File.Copy(nuget, "./src/.nuget/NuGet.exe", true)

// Read release notes document
let release = ReleaseNotesHelper.parseReleaseNotes (File.ReadLines "doc/ReleaseNotes.md")

let MyTarget name body =
    Target name body
    Target (sprintf "%s_single" name) body 


type BuildParams =
    {
        CustomBuildName : string
    }

let buildApp (buildParams:BuildParams) =
    let buildDir = buildDir @@ buildParams.CustomBuildName
    CleanDirs [ buildDir ]
    // build app
    let files = !! "src/source/**/*.csproj"
    files
        |> MSBuild buildDir "Build" 
            [   "Configuration", buildMode
                "CustomBuildName", buildParams.CustomBuildName ]
        |> Log "AppBuild-Output: "

let buildTests (buildParams:BuildParams) =
    let testDir = testDir @@ buildParams.CustomBuildName
    CleanDirs [ testDir ]
    // build tests
    let files = !! "src/test/**/Test.*.csproj"
    files
        |> MSBuild testDir "Build" 
            [   "Configuration", buildMode
                "CustomBuildName", buildParams.CustomBuildName ]
        |> Log "TestBuild-Output: "
    
let runTests  (buildParams:BuildParams) =
    let testDir = testDir @@ buildParams.CustomBuildName
    let logs = System.IO.Path.Combine(testDir, "logs")
    System.IO.Directory.CreateDirectory(logs) |> ignore
    let files = 
        !! (testDir + "/Test.*.dll")
    files
        |> NUnit (fun p ->
            {p with
                //NUnitParams.WorkingDir = working
                //ExcludeCategory = if isMono then "VBNET" else ""
                ProcessModel = 
                    // Because the default nunit-console.exe.config doesn't use .net 4...
                    if isMono then NUnitProcessModel.SingleProcessModel else NUnitProcessModel.DefaultProcessModel
                WorkingDir = testDir
                StopOnError = true
                TimeOut = System.TimeSpan.FromMinutes 30.0
                Framework = "4.0"
                DisableShadowCopy = true;
                OutputFile = "logs/TestResults.xml" })

    
let net40Params = { CustomBuildName = "net40" }
let net45Params = { CustomBuildName = "net45" }
let sl40Params = { CustomBuildName = "sl40" }


// Documentation 
let buildDocumentationTarget target =
    trace (sprintf "Building documentation (%s), this could take some time, please wait..." target)
    let b, s = executeFSI "." "generateDocs.fsx" ["target", target]
    for l in s do
        (if l.IsError then traceError else trace) (sprintf "DOCS: %s" l.Message)
    if not b then
        failwith "documentation failed"
    ()
