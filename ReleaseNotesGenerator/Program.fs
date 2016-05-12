
open Octokit
open Octokit.Reactive
open System.Reactive.Linq;

open GitHub
open Observables
open CommandLine

let formatSection str items =
    printf "For group '%s':\r\n" str
    items |> Seq.iter (fun item -> printf " - %s - #%d via @%s\r\n"  item.Title item.Id item.Author)
    printf "\r\n"

let writeSection results name = 
   results
        |> Seq.tryFind(fun (str, items) -> str = name)
        |> Option.iter (fun (str, items) -> formatSection str items)

let writeSkippedList (items:seq<_>)=
    printf "Entries skipped: %d\r\n" (items |> Seq.toList |> fun f -> f.Length)
    printf "\r\n"

[<EntryPoint>]
let main argv = 

   let token = System.Environment.GetEnvironmentVariable "OCTOKIT_OAUTHTOKEN"
   
   let defaultOptions = {
       whatif = false;
       repository = "";
       token = token;
       branch = "master"
       }

   let argsProcessed = parseCommandLine (Array.toList argv) defaultOptions

   // TODO: be way more cleverer about what we support
   let index = argsProcessed.repository.IndexOf('/')

   let owner = argsProcessed.repository.Substring(0, index)
   let name = argsProcessed.repository.Substring (index+1)

   let client = ObservableGitHubClient(new ProductHeaderValue("Hermes"))
   client.Connection.Credentials<-Credentials(token)

   // curried functions
   let resolvePullRequestUsingId =  resolvePullRequest client owner name
   let getPullRequestDetailsUsingResponse = getPullRequestSummary client owner name

   // the actual heavy-lifting
   let latestRelease = await (client.Repository.Release.GetAll(owner,  name).Take 1)

   let compare = await (client.Repository.Commit.Compare(owner, name, latestRelease.TagName, argsProcessed.branch))

   let mergedPullRequests = findMergePullRequests compare

   let results = mergedPullRequests
                    |> Seq.map (fun id -> await (resolvePullRequestUsingId id))
                    |> Seq.map (fun pr -> await (getPullRequestDetailsUsingResponse pr))
                    |> Seq.groupBy (fun f -> getGroupingForPullRequest f.Tags)
                    |> Seq.toArray

   writeSection results "feature"
   writeSection results "bugfix"
   writeSection results "other"

   results
        |> Seq.tryFind(fun (str, items) -> str = "skip-release-notes")
        |> Option.iter (fun (str, items) -> writeSkippedList items)

   printfn "%A" argv
   0 // return an integer exit code
