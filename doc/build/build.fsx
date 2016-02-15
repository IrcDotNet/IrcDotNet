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

#load @"buildConfig.fsx"
open BuildConfig

open System.Collections.Generic
open System.IO

open Fake
open Fake.Git
open Fake.FSharpFormatting
open AssemblyInfoFile


let MyTarget name body =
    Target name (fun _ -> body false)
    let single = (sprintf "%s_single" name)
    Target single (fun _ -> body true) 

// Targets
MyTarget "Clean" (fun _ ->
    CleanDirs [ buildDir; releaseDir ]
)

// Documentation 

MyTarget "GithubDoc" (fun _ -> buildDocumentationTarget "GithubDoc")

MyTarget "LocalDoc" (fun _ -> 
    buildDocumentationTarget "LocalDoc"
    trace (sprintf "Local documentation has been finished, you can view it by opening %s in your browser!" (Path.GetFullPath (outDocDir @@ "local" @@ "html" @@ "index.html")))
)


MyTarget "ReleaseGithubDoc" (fun isSingle ->
    let repro = (sprintf "git@github.com:%s/%s.git" github_user github_project)
    let doAction =
        if isSingle then true
        else
            printf "update github docs to %s? (y,n): " repro
            let line = System.Console.ReadLine()
            line = "y"
    if doAction then
        CleanDir "gh-pages"
        cloneSingleBranch "" repro "gh-pages" "gh-pages"
        fullclean "gh-pages"
        CopyRecursive ("release"@@"documentation"@@(sprintf "%s.github.io" github_user)@@"html") "gh-pages" true |> printfn "%A"
        StageAll "gh-pages"
        Commit "gh-pages" (sprintf "Update generated documentation")
        printf "gh-pages branch updated in the gh-pages directory, push that branch to %s now? (y,n): " repro
        let line = System.Console.ReadLine()
        if line = "y" then
            Branches.pushBranch "gh-pages" "origin" "gh-pages"
)

Target "All" (fun _ ->
    trace "All finished!"
)

// Dependencies
"Clean"
  ==> "LocalDoc"
  ==> "All"
 
"All" 
  ==> "GithubDoc"
  ==> "ReleaseGithubDoc"

// start build
RunTargetOrDefault "All"
