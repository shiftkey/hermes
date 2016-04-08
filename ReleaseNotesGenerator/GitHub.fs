module GitHub

open System
open System.Reactive.Linq
open System.Text.RegularExpressions
open Octokit
open Octokit.Reactive

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