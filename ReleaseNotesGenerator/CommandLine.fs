
module CommandLine

type CommandLineOptions = {
    repository: string;
    token: string;
    whatif: bool;
    }

let rec parseCommandLine args optionsSoFar = 
    match args with 
    | [] -> 
        optionsSoFar  

    | "/whatif"::xs -> 
        let newOptionsSoFar = { optionsSoFar with whatif=true}
        parseCommandLine xs newOptionsSoFar 

    | "/token"::xs -> 
        match xs with
        | x::xss ->
            if x.StartsWith("/") then
                eprintfn "Token needs a second argument"
                parseCommandLine xs optionsSoFar 
            else 
                let newOptionsSoFar = { optionsSoFar with token=x}
                parseCommandLine xss newOptionsSoFar 
        | [] -> 
            eprintfn "Token needs a second argument"
            parseCommandLine xs optionsSoFar 
        | _ -> 
            eprintfn "Token needs a second argument"
            parseCommandLine xs optionsSoFar 

    | "/repo"::xs -> 
        match xs with
        | x::xss -> 
            if x.StartsWith("/") then
                eprintfn "Repository needs a second argument"
                parseCommandLine xs optionsSoFar 
            else 
                let newOptionsSoFar = { optionsSoFar with repository=x}
                parseCommandLine xss newOptionsSoFar 
        | [] -> 
            eprintfn "Repository needs a second argument"
            parseCommandLine xs optionsSoFar 
        | _ -> 
            eprintfn "Repository needs a second argument"
            parseCommandLine xs optionsSoFar 

    | x::xs -> 
        eprintfn "Option '%s' is unrecognized" x
        parseCommandLine xs optionsSoFar 