open System
open System.Reactive.Linq
open System.Text.RegularExpressions
open Octokit
open Octokit.Reactive

// TODO: move this stuff out to a separate module for Observable-esque stuff

let synchronize f = 
  let ctx = System.Threading.SynchronizationContext.Current 
  f (fun g arg ->
    let nctx = System.Threading.SynchronizationContext.Current 
    if ctx <> null && ctx <> nctx then ctx.Post((fun _ -> g(arg)), null)
    else g(arg) )

type Microsoft.FSharp.Control.Async with 
  static member AwaitObservable(ev1:IObservable<'a>) : Async<_> =
    synchronize (fun f ->
      Async.FromContinuations((fun (cont,econt,ccont) -> 
        let rec callback = (fun value ->
          remover.Dispose()
          f cont value )
        and remover : IDisposable  = ev1.Subscribe(callback) 
        () )))

let await (obs:IObservable<_>) = Async.RunSynchronously (Async.AwaitObservable obs)

// TODO: extract this to some Octokit-related functionality

let mergeCommitRegex = Regex(@"Merge pull request #(?<id>\d{1,})", RegexOptions.Compiled);

let findMergePullRequests (compareResult:Octokit.CompareResult) =
   compareResult.Commits
    |> Seq.map (fun c -> c.Commit.Message)
    |> Seq.filter (fun message -> mergeCommitRegex.IsMatch(message))
    |> Seq.map (fun message -> mergeCommitRegex.Match(message).Groups.["id"].Value)
    |> Seq.map (fun id -> Int32.Parse(id))

let resolvePullRequest (client:ObservableGitHubClient) owner name id = 
    client.PullRequest.Get (owner, name, id)

let getLabelsForPullRequest (client:ObservableGitHubClient) owner name number =
     client.Issue.Labels.GetAllForIssue(owner, name, number)

type PullRequestSummary = { Title: string; Id: int; Author: string; Tags: string[] }

let getPullRequestSummary (client:ObservableGitHubClient) owner name (pr:PullRequest) = 
    let labels = getLabelsForPullRequest client owner name pr.Number
    let labelNames = labels.Select(fun l -> l.Name).ToArray()
    labelNames.Select(fun l -> { Title = pr.Title; Id = pr.Number; Author = pr.User.Login; Tags = l; })

let getGroupingForPullRequest (list:string[]) = 
    match list.Length with
      | 0 -> "other"
      | _ -> list.[0]

let writeFeatures str items =
    printf "For group '%s':\r\n" str
    items |> Seq.iter (fun item -> printf " - %s - #%d via @%s\r\n"  item.Title item.Id item.Author)
    printf "\r\n"

let writeSection results name = 
   results
        |> Seq.tryFind(fun (str, items) -> str = name)
        |> Option.iter (fun (str, items) -> writeFeatures str items)

let writeSkippedList (items:List<_>)=
    printf "Entries skipped '%d':\r\n" items.Length
    printf "\r\n"

[<EntryPoint>]
let main argv = 
   let token = System.Environment.GetEnvironmentVariable "OCTOKIT_OAUTHTOKEN"
   let client = ObservableGitHubClient(new ProductHeaderValue("Hermes"))
   client.Connection.Credentials<-Credentials(token)

   // TODO: accept these as arguments

   let owner = "octokit"
   let name = "octokit.net"

   // curried functions
   let resolvePullRequestUsingId =  resolvePullRequest client owner name
   let getPullRequestDetailsUsingResponse = getPullRequestSummary client owner name

   // the actual heavy-lifting
   let latestRelease = await (client.Repository.Release.GetAll(owner,  name).Take 1)

   let compare = await (client.Repository.Commit.Compare(owner, name, latestRelease.TagName, "master"))

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
        |> Option.iter (fun (str, items) -> writeSkippedList (items |> Seq.toList))

   printfn "%A" argv
   0 // return an integer exit code
