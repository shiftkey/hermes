module ReleaseNotesGenerator.CommandLineSpecs

open CommandLine
open NUnit.Framework
open FsUnit


let getDefaultOptions () =
    {
       whatif = false;
       repository = "";
       token = "";
       branch = "master"
     }

[<Test>]
let ``branch parameter can be parsed``() = 
    let defaultOptions = getDefaultOptions()
    let newBranchName = "newBranch"
    let args = ["/branch"; newBranchName;]
    (parseCommandLine args defaultOptions ).branch |> should equal newBranchName