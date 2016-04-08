module GitHub

open System
open System.Reactive.Linq
open System.Text.RegularExpressions
open Octokit
open Octokit.Reactive

open Observables

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

let getCommentsForPullRequest (client:ObservableGitHubClient) owner name number =
     client.Issue.Comment.GetAllForIssue(owner, name, number)

let getSummaryForPullRequest client owner name number defaultText =
     let all = getCommentsForPullRequest client owner name number
     // TODO: should look at author or something
     let formattedMessage = all.LastOrDefaultAsync(fun c -> c.Body.StartsWith "release_notes: ")
                               .Where(fun c -> c <> null)
                               .Select(fun c -> c.Body.Replace("release_notes: ", ""))
     // TODO: this ensures we only return one value
     Observable.Concat(formattedMessage, Observable.Return(defaultText)).Take(1)

type PullRequestSummary = { Title: string; Id: int; Author: string; Tags: string[] }

let getPullRequestSummary (client:ObservableGitHubClient) owner name (pr:PullRequest) = 
    let labels = getLabelsForPullRequest client owner name pr.Number
    let labelNames = labels.Select(fun l -> l.Name).ToArray()
    let title = await (getSummaryForPullRequest client owner name pr.Number pr.Title)
    labelNames.Select(fun l -> { Title = title; Id = pr.Number; Author = pr.User.Login; Tags = l; })

let getGroupingForPullRequest (list:string[]) = 
    match list.Length with
      | 0 -> "other"
      | _ -> list.[0]