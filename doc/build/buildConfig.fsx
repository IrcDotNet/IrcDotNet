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
let projectName = "IRC.NET"
let copyrightNotice = "IRC.NET is copyright © 2011-2016 Alex Regueiro, Christian Stewart"
let projectSummary = "A complete IRC (Internet Relay Chat) client library for the .NET Framework"
let projectDescription = "IRC.NET aims to provide a complete and efficient implementation of the protocol as described in RFCs 1459 and 2812, as well as de-facto modern features of the protocol."
let authors = ["Alex Regueiro", "Christian Stewart"]
let page_author = "Matthias Dittrich"
let mail = "christian+ircdotnet@paral.in"
let version = "0.5.0"
let version_nuget = version

let buildDir = "./build/"
let releaseDir = "./release/"
let outDocDir = "./release/documentation/"
let docTemplatesDir = "../content/templates/"
let nugetDir  = "./.nuget/"
let packageDir  = "./.nuget/packages"

let github_user = "IrcDotNet"
let github_project = "IrcDotNet"
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
    

// Read release notes document
let MyTarget name body =
    Target name body
    Target (sprintf "%s_single" name) body 

// Documentation 
let buildDocumentationTarget target =
    trace (sprintf "Building documentation (%s), this could take some time, please wait..." target)
    let b, s = executeFSI "." "generateDocs.fsx" ["target", target]
    for l in s do
        (if l.IsError then traceError else trace) (sprintf "DOCS: %s" l.Message)
    if not b then
        failwith "documentation failed"
    ()
