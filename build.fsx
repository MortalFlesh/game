#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"
#r "netstandard"

open System

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Tools.Git

type ToolDir =
    /// Global tool dir must be in PATH - ${PATH}:/root/.dotnet/tools
    | Global
    /// Just a dir name, the location will be used as: ./{LocalDirName}
    | Local of string

// ========================================================================================================
// === F# / SAFE app fake build =================================================================== 3.0.0 =
// --------------------------------------------------------------------------------------------------------
// Options:
//  - no-lint    - lint will be executed, but the result is not validated
// --------------------------------------------------------------------------------------------------------
// Table of contents:
//      1. Information about project, configuration
//      2. Utilities, DotnetCore functions
//      3. FAKE targets
//      4. FAKE targets hierarchy
// ========================================================================================================

// --------------------------------------------------------------------------------------------------------
// 1. Information about the project to be used at NuGet and in AssemblyInfo files and other FAKE configuration
// --------------------------------------------------------------------------------------------------------

let project = "MF.Game"
let summary = "Multiplayer game attempt :)"

let changeLog = None
let gitCommit = Information.getCurrentSHA1(".")
let gitBranch = Information.getBranchName(".")

let toolsDir = Global

Target.initEnvironment ()

let sharedPath = Path.getFullName "src/Shared"
let serverPath = Path.getFullName "src/Server"
let clientPath = Path.getFullName "src/Client"
let deployDir = Path.getFullName "deploy"
let sharedTestsPath = Path.getFullName "tests/Shared"
let serverTestsPath = Path.getFullName "tests/Server"
let clientTestsPath = Path.getFullName "tests/Client"

// --------------------------------------------------------------------------------------------------------
// 2. Utilities, DotnetCore functions, etc.
// --------------------------------------------------------------------------------------------------------

let npm args workingDir =
    let npmPath =
        match ProcessUtils.tryFindFileOnPath "npm" with
        | Some path -> path
        | None ->
            "npm was not found in path. Please install it and make sure it's available from your path. " +
            "See https://safe-stack.github.io/docs/quickstart/#install-pre-requisites for more info"
            |> failwith

    let arguments = args |> String.split ' ' |> Arguments.OfArgs

    Command.RawCommand (npmPath, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

[<AutoOpen>]
module private Utils =
    let tee f a =
        f a
        a

    let skipOn option action p =
        if p.Context.Arguments |> Seq.contains option
        then Trace.tracefn "Skipped ..."
        else action p

    module DotnetCore =
        let run cmd workingDir =
            let options =
                DotNet.Options.withWorkingDirectory workingDir
                >> DotNet.Options.withRedirectOutput true

            DotNet.exec options cmd ""

        let runOrFail cmd workingDir =
            run cmd workingDir
            |> tee (fun result ->
                if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir
            )
            |> ignore

        let runInRoot cmd = run cmd "."
        let runInRootOrFail cmd = runOrFail cmd "."

        let installOrUpdateTool toolDir tool =
            let toolCommand action =
                match toolDir with
                | Global -> sprintf "tool %s --global %s" action tool
                | Local dir -> sprintf "tool %s --tool-path ./%s %s" action dir tool

            match runInRoot (toolCommand "install") with
            | { ExitCode = code } when code <> 0 ->
                match runInRoot (toolCommand "update") with
                | { ExitCode = code } when code <> 0 -> Trace.tracefn "Warning: Install and update of %A has failed." tool
                | _ -> ()
            | _ -> ()

        let execute command args (dir: string) =
            let cmd =
                sprintf "%s/%s"
                    (dir.TrimEnd('/'))
                    command

            let processInfo = Diagnostics.ProcessStartInfo(cmd)
            processInfo.RedirectStandardOutput <- true
            processInfo.RedirectStandardError <- true
            processInfo.UseShellExecute <- false
            processInfo.CreateNoWindow <- true
            processInfo.Arguments <- args |> String.concat " "

            use proc =
                new Diagnostics.Process(
                    StartInfo = processInfo
                )
            if proc.Start() |> not then failwith "Process was not started."
            proc.WaitForExit()

            if proc.ExitCode <> 0 then failwithf "Command '%s' failed in %s." command dir
            (proc.StandardOutput.ReadToEnd(), proc.StandardError.ReadToEnd())

    let runInParallel tasks =
        tasks
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore

    /// A shortcut for the most cases of dotnet core usage
    let dotnet = DotnetCore.runOrFail

let envVar name =
    if Environment.hasEnvironVar(name)
        then Environment.environVar(name) |> Some
        else None

let stringToOption = function
    | null | "" -> None
    | string -> Some string

[<RequireQualifiedAccess>]
module Option =
    let mapNone f = function
        | Some v -> v
        | None -> f None

    let bindNone f = function
        | Some v -> Some v
        | None -> f None

// --------------------------------------------------------------------------------------------------------
// 3. Targets for FAKE
// --------------------------------------------------------------------------------------------------------

Target.create "Clean" (fun _ ->
    !! "./**/bin/Release"
    ++ "./**/bin/Debug"
    ++ "./**/obj"
    ++ "./**/.ionide"
    |> Shell.cleanDirs
)

Target.create "SafeClean" (fun _ ->
    [ deployDir ]
    |> Shell.cleanDirs

    dotnet "fable clean --yes" clientPath
)

Target.create "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        let now = DateTime.Now

        let release =
            changeLog
            |> Option.bind (fun changeLog ->
                try ReleaseNotes.parse (System.IO.File.ReadAllLines changeLog |> Seq.filter ((<>) "## Unreleased")) |> Some
                with _ -> None
            )

        let gitValue fallbackEnvironmentVariableNames initialValue =
            initialValue
            |> String.replace "NoBranch" ""
            |> stringToOption
            |> Option.bindNone (fun _ -> fallbackEnvironmentVariableNames |> List.tryPick envVar)
            |> Option.defaultValue "unknown"

        [
            AssemblyInfo.Title projectName
            AssemblyInfo.Product project
            AssemblyInfo.Description summary

            match release with
            | Some release ->
                AssemblyInfo.Version release.AssemblyVersion
                AssemblyInfo.FileVersion release.AssemblyVersion
            | _ ->
                AssemblyInfo.Version "1.0"
                AssemblyInfo.FileVersion "1.0"

            AssemblyInfo.InternalsVisibleTo "tests"
            AssemblyInfo.Metadata("gitbranch", gitBranch |> gitValue [ "GIT_BRANCH"; "branch" ])
            AssemblyInfo.Metadata("gitcommit", gitCommit |> gitValue [ "GIT_COMMIT"; "commit" ])
            AssemblyInfo.Metadata("buildNumber", "BUILD_NUMBER" |> envVar |> Option.defaultValue "-")
            AssemblyInfo.Metadata("createdAt", now.ToString("yyyy-MM-dd HH:mm:ss"))
            AssemblyInfo.Metadata("SafeTemplateVersion", "2.2.0")
        ]

    let getProjectDetails (projectPath: string) =
        let projectName = IO.Path.GetFileNameWithoutExtension(projectPath)
        (
            projectPath,
            projectName,
            IO.Path.GetDirectoryName(projectPath),
            (getAssemblyInfoAttributes projectName)
        )

    !! "src/**/*.fsproj"
    ++ "tests/**/*.fsproj"
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (_, _, folderName, attributes) ->
        AssemblyInfoFile.createFSharp (folderName </> "AssemblyInfo.fs") attributes
    )
)

Target.create "InstallClient" (fun _ ->
    // printfn "Update npm"

    printfn "Npm version:"
    npm "--version" __SOURCE_DIRECTORY__
    npm "install" __SOURCE_DIRECTORY__
)

Target.create "Lint" <| skipOn "no-lint" (fun _ ->
    DotnetCore.installOrUpdateTool toolsDir "dotnet-fsharplint"

    let checkResult (messages: string list) =
        let rec check: string list -> unit = function
            | [] -> failwithf "Lint does not yield a summary."
            | head :: rest ->
                if head.Contains "Summary" then
                    match head.Replace("= ", "").Replace(" =", "").Replace("=", "").Replace("Summary: ", "") with
                    | "0 warnings" -> Trace.tracefn "Lint: OK"
                    | warnings -> failwithf "Lint ends up with %s." warnings
                else check rest
        messages
        |> List.rev
        |> check

    !! "src/**/*.fsproj"
    |> Seq.map (fun fsproj ->
        match toolsDir with
        | Global ->
            DotnetCore.runInRoot (sprintf "fsharplint lint %s" fsproj)
            |> fun (result: ProcessResult) -> result.Messages
        | Local dir ->
            DotnetCore.execute "dotnet-fsharplint" ["lint"; fsproj] dir
            |> fst
            |> tee (Trace.tracefn "%s")
            |> String.split '\n'
            |> Seq.toList
    )
    |> Seq.iter checkResult
)

Target.create "Run" (fun _ ->
    dotnet "build" sharedPath

    runInParallel [
        async { dotnet "watch run" serverPath }
        async { dotnet "fable watch -o output -s --run npm run start" clientPath }
    ]
)

Target.create "Bundle" (fun _ ->
    Directory.ensure (deployDir </> "configuration")

    runInParallel [
        async { dotnet (sprintf "publish -c Release -o \"%s\"" deployDir) serverPath }
        async { dotnet "fable -o output -s --run npm run build" clientPath }
    ]
)

Target.create "Tests" (fun _ ->
    dotnet "build" sharedTestsPath

    runInParallel [
        async { dotnet "run" serverTestsPath }
        async { dotnet "fable watch -o output -s --run npm run test:live" clientTestsPath }
    ]
)

Target.create "WatchTests" (fun _ ->
    dotnet "build" sharedTestsPath

    runInParallel [
        async { dotnet "watch run" serverTestsPath }
        async { npm "run test:live" "." }
    ]
)

// --------------------------------------------------------------------------------------------------------
// 4. FAKE targets hierarchy
// --------------------------------------------------------------------------------------------------------

open Fake.Core.TargetOperators

"SafeClean"
    ==> "Clean"

"SafeClean"
    ==> "AssemblyInfo"
    ==> "InstallClient"
    ==> "Bundle"

"SafeClean"
    ==> "InstallClient"
    ==> "Run"

"SafeClean"
    ==> "InstallClient"
    ==> "Lint"
    ==> "Tests" <=> "WatchTests"

Target.runOrDefaultWithArguments "Bundle"
