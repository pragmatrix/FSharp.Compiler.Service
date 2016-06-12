﻿
#if INTERACTIVE
#r "../../bin/v4.5/FSharp.Compiler.Service.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
#load "FsUnit.fs"
#load "Common.fs"
#else
module FSharp.Compiler.Service.Tests.FsiTests
#endif

open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.Interactive.Shell
open Microsoft.FSharp.Compiler.SourceCodeServices

open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Text

// Intialize output and input streams
let inStream = new StringReader("")
let outStream = new CompilerOutputStream()
let errStream = new CompilerOutputStream()

// Build command line arguments & start FSI session
let argv = [| "C:\\fsi.exe" |]
let allArgs = Array.append argv [|"--noninteractive"|]

#if DOTNETCORE
let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
#else
let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration(fsi)
#endif
let fsiSession = FsiEvaluationSession.Create(fsiConfig, allArgs, inStream, new StreamWriter(outStream), new StreamWriter(errStream))  

/// Evaluate expression & return the result
let evalExpression text =
    match fsiSession.EvalExpression(text) with
    | Some value -> sprintf "%A" value.ReflectionValue
    | None -> sprintf "null or no result"

let formatErrors (errs: FSharpErrorInfo[]) = 
   [ for err in errs do yield sprintf "%s %d,%d - %d,%d; %s" (match err.Severity with FSharpErrorSeverity.Error -> "error" | FSharpErrorSeverity.Warning -> "warning") err.StartLineAlternate err.StartColumn err.EndLineAlternate err.EndColumn err.Message ]

let showErrorsAndResult (x, errs) = 
   [ match x with 
       | Choice1Of2 res -> yield sprintf "result %A" res
       | Choice2Of2 (exn:exn) -> yield sprintf "exception %s" exn.Message
     yield! formatErrors errs ]

let showErrors (x, errs: FSharpErrorInfo[]) = 
   [ match x with 
       | Choice1Of2 () -> ()
       | Choice2Of2 (exn:exn) -> yield sprintf "exception %s" exn.Message
     yield! formatErrors errs ]

/// Evaluate expression & return the result
let evalExpressionNonThrowing text =
   let res, errs = fsiSession.EvalExpressionNonThrowing(text)
   [ match res with 
       | Choice1Of2 valueOpt -> 
            match valueOpt with 
            | Some value -> yield sprintf "%A" value.ReflectionValue
            | None -> yield sprintf "null or no result"
       | Choice2Of2 (exn:exn) -> yield sprintf "exception %s" exn.Message
     yield! formatErrors errs ]

// For some reason NUnit doesn't like running these FsiEvaluationSession tests. We need to work out why.
//#if INTERACTIVE
[<Test>]
let ``EvalExpression test 1``() = 
    evalExpression "42+1" |> shouldEqual "43"

[<Test>]
let ``EvalExpression test 1 nothrow``() = 
    evalExpressionNonThrowing "42+1" |> shouldEqual ["43"]

[<Test>]
// 'fsi' can be evaluated because we passed it in explicitly up above
let ``EvalExpression fsi test``() = 
    evalExpression "fsi" |> shouldEqual "Microsoft.FSharp.Compiler.Interactive.InteractiveSession"

[<Test>]
// 'fsi' can be evaluated because we passed it in explicitly up above
let ``EvalExpression fsi test 2``() = 
    fsiSession.EvalInteraction "fsi.AddPrinter |> ignore" 

[<Test>]
// 'fsi' can be evaluated because we passed it in explicitly up above
let ``EvalExpression fsi test 2 non throwing``() = 
    fsiSession.EvalInteractionNonThrowing "fsi.AddPrinter |> ignore" 
       |> showErrors
       |> shouldEqual []


[<Test>]
let ``EvalExpression typecheck failure``() = 
    (try evalExpression "42+1.0"  |> ignore
         false
     with e -> true)
    |> shouldEqual true

[<Test>]
let ``EvalExpression typecheck failure nothrow``() = 
    evalExpressionNonThrowing("42+1.0")
    |> shouldEqual 
          ["exception Operation could not be completed due to earlier error";
           "error 1,3 - 1,6; The type 'float' does not match the type 'int'";
           "error 1,2 - 1,3; The type 'float' does not match the type 'int'"]


[<Test>]
let ``EvalExpression function value 1``() = 
    fsiSession.EvalExpression "(fun x -> x + 1)"  |> fun s -> s.IsSome
    |> shouldEqual true

[<Test>]
let ``EvalExpression function value 2``() = 
    fsiSession.EvalExpression "fun x -> x + 1"  |> fun s -> s.IsSome
    |> shouldEqual true

[<Test>]
let ``EvalExpression function value 3``() = 
    fsiSession.EvalExpression "incr"  |> fun s -> s.IsSome
    |> shouldEqual true

[<Test; Ignore("Failing test for #135")>]
let ``EvalExpression function value 4``() = 
    fsiSession.EvalInteraction  "let hello(s : System.IO.TextReader) = printfn \"Hello World\""
    fsiSession.EvalExpression "hello"  |> fun s -> s.IsSome
    |> shouldEqual true

[<Test>]
let ``EvalExpression runtime failure``() = 
    (try evalExpression """ (failwith "fail" : int) """  |> ignore
         false
     with e -> true)
    |> shouldEqual true

[<Test>]
let ``EvalExpression parse failure``() = 
    (try evalExpression """ let let let let x = 1 """  |> ignore
         false
     with e -> true)
    |> shouldEqual true

[<Test>]
let ``EvalExpression parse failure nothrow``() = 
    evalExpressionNonThrowing """ let let let let x = 1 """  
    |> shouldEqual 
          ["exception Operation could not be completed due to earlier error";
           "error 1,5 - 1,8; Unexpected keyword 'let' or 'use' in binding";
           "error 1,1 - 1,4; Block following this 'let' is unfinished. Expect an expression."]

[<Test>]
let ``EvalInteraction typecheck failure``() = 
    (try fsiSession.EvalInteraction "let x = 42+1.0"  |> ignore
         false
     with e -> true)
    |> shouldEqual true

[<Test>]
let ``EvalInteraction typecheck failure nothrow``() = 
    fsiSession.EvalInteractionNonThrowing "let x = 42+1.0"  
    |> showErrors
    |> shouldEqual
      ["exception Operation could not be completed due to earlier error";
       "error 1,11 - 1,14; The type 'float' does not match the type 'int'";
       "error 1,10 - 1,11; The type 'float' does not match the type 'int'"]

[<Test>]
let ``EvalInteraction runtime failure``() = 
    (try fsiSession.EvalInteraction """let x = (failwith "fail" : int) """  |> ignore
         false
     with e -> true)
    |> shouldEqual true

[<Test>]
let ``EvalInteraction runtime failure nothrow``() = 
    fsiSession.EvalInteractionNonThrowing """let x = (failwith "fail" : int) """  
    |> showErrors
    |> shouldEqual ["exception fail"]

[<Test>]
let ``EvalInteraction parse failure``() = 
    (try fsiSession.EvalInteraction """ let let let let x =  """  |> ignore
         false
     with e -> true)
    |> shouldEqual false  // EvalInteraction doesn't fail for parse failures, it just reports errors.

[<Test>]
let ``EvalInteraction parse failure nothrow``() = 
    fsiSession.EvalInteractionNonThrowing """ let let let let x =  """  
    |> showErrors
    |> shouldEqual 
          ["exception Operation could not be completed due to earlier error";
           "error 1,5 - 1,8; Unexpected keyword 'let' or 'use' in binding";
           "warning 1,0 - 1,22; Possible incorrect indentation: this token is offside of context started at position (1:14). Try indenting this token further or using standard formatting conventions.";
           "warning 1,22 - 1,22; Possible incorrect indentation: this token is offside of context started at position (1:14). Try indenting this token further or using standard formatting conventions."]

[<Test>]
let ``PartialAssemblySignatureUpdated test``() = 
    let count = ref 0 
    fsiSession.PartialAssemblySignatureUpdated.Add(fun x -> count := count.Value + 1)
    count.Value |> shouldEqual 0
    fsiSession.EvalInteraction """ let x = 1 """  
    count.Value |> shouldEqual 1
    fsiSession.EvalInteraction """ let x = 1 """  
    count.Value |> shouldEqual 2


[<Test>]
let ``ParseAndCheckInteraction test 1``() = 
    fsiSession.EvalInteraction """ let xxxxxx = 1 """  
    fsiSession.EvalInteraction """ type CCCC() = member x.MMMMM()  = 1 + 1 """  
    let untypedResults, typedResults, _ = fsiSession.ParseAndCheckInteraction("xxxxxx")
    untypedResults.FileName |> shouldEqual "stdin.fsx"
    untypedResults.Errors.Length |> shouldEqual 0
    untypedResults.ParseHadErrors |> shouldEqual false

    // Check we can't get a declaration location for text in the F# interactive state (because the file doesn't exist)
    // TODO: check that if we use # line directives, then the file will exist correctly
    let identToken = FSharpTokenTag.IDENT
    typedResults.GetDeclarationLocationAlternate(1,6,"xxxxxx",["xxxxxx"]) |> Async.RunSynchronously |> shouldEqual (FSharpFindDeclResult.DeclNotFound  FSharpFindDeclFailureReason.NoSourceCode) 

    // Check we can get a tooltip for text in the F# interactive state
    let tooltip = 
        match typedResults.GetToolTipTextAlternate(1,6,"xxxxxx",["xxxxxx"],identToken)  |> Async.RunSynchronously with 
        | FSharpToolTipText [FSharpToolTipElement.Single(text, FSharpXmlDoc.None)] -> text
        | _ -> failwith "incorrect tool tip"

    Assert.True(tooltip.Contains("val xxxxxx : int"))

[<Test>]
let ``Bad arguments to session creation 1``() = 
    let inStream = new StringReader("")
    let outStream = new CompilerOutputStream()
    let errStream = new CompilerOutputStream()
    let errWriter = new StreamWriter(errStream)
    let fsiSession = 
        try 
           FsiEvaluationSession.Create(fsiConfig, [| "fsi.exe"; "-r:nonexistent.dll" |], inStream, new StreamWriter(outStream), errWriter) |> ignore
           false
        with _ -> true
    Assert.True fsiSession
    Assert.False (String.IsNullOrEmpty (errStream.Read())) // error stream contains some output
    Assert.True (String.IsNullOrEmpty (outStream.Read())) // output stream contains no output

[<Test>]
let ``Bad arguments to session creation 2``() = 
    let inStream = new StringReader("")
    let outStream = new CompilerOutputStream()
    let errStream = new CompilerOutputStream()
    let errWriter = new StreamWriter(errStream)
    let fsiSession = 
        try 
           FsiEvaluationSession.Create(fsiConfig, [| "fsi.exe"; "-badarg" |], inStream, new StreamWriter(outStream), errWriter) |> ignore
           false
        with _ -> true
    Assert.True fsiSession
    Assert.False (String.IsNullOrEmpty (errStream.Read())) // error stream contains some output
    Assert.True (String.IsNullOrEmpty (outStream.Read())) // output stream contains no output

[<Test>]
// Regression test for #184
let ``EvalScript accepts paths verbatim``() =
    // Path contains escape sequences (\b and \n)
    // Let's ensure the exception thrown (if any) is FileNameNotResolved
    (try
        let scriptPath = @"C:\bad\path\no\donut.fsx"
        fsiSession.EvalScript scriptPath |> ignore
        false
     with
        | e ->
            true)
    |> shouldEqual true

[<Test>]
// Regression test for #184
let ``EvalScript accepts paths verbatim nothrow``() =
    // Path contains escape sequences (\b and \n)
    // Let's ensure the exception thrown (if any) is FileNameNotResolved
    let scriptPath = @"C:\bad\path\no\donut.fsx"
    fsiSession.EvalScriptNonThrowing scriptPath 
    |> showErrors 
    |> List.map (fun s -> s.[0..20])  // avoid seeing the hardwired paths
    |> Seq.toList
    |> shouldEqual 
          ["exception Operation c";
           "error 1,0 - 1,33; Una"]


[<Test>]
let ``Disposing interactive session (collectible)``() =

    let createSession i =
        let defaultArgs = [|"fsi.exe";"--noninteractive";"--nologo";"--gui-"|]
        let sbOut = StringBuilder()
        use inStream = new StringReader("")
        use outStream = new StringWriter(sbOut)
        let sbErr = StringBuilder("")
        use errStream = new StringWriter(sbErr)

        let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
        use session = FsiEvaluationSession.Create(fsiConfig, defaultArgs, inStream, outStream, errStream, collectible=true)
        
        session.EvalInteraction <| sprintf "let x%i = 42" i

    // Dynamic assemblies should be collected and handle count should not be increased
    for i in 1 .. 50 do
        printfn "iteration %d" i
        createSession i

[<Test>]
let ``interactive session events``() =

        let defaultArgs = [|"fsi.exe";"--noninteractive";"--nologo";"--gui-"|]
        let sbOut = StringBuilder()
        use inStream = new StringReader("")
        use outStream = new StringWriter(sbOut)
        let sbErr = StringBuilder("")
        use errStream = new StringWriter(sbErr)

        let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
        let evals = ResizeArray()
        use evaluator = fsiConfig.OnEvaluation.Subscribe (fun eval -> evals.Add (eval.FsiValue, eval.Name, eval.SymbolUse))

        use session = FsiEvaluationSession.Create(fsiConfig, defaultArgs, inStream, outStream, errStream, collectible=true)
        session.EvalInteraction  "let x = 42"
        
        let value, name, symbol = evals.[0]
        name |> should equal "x"
        value.IsSome |> should equal true
        value.Value.ReflectionValue |> should equal 42
        symbol.Symbol.GetType() |> should equal typeof<FSharpMemberOrFunctionOrValue>
        symbol.Symbol.DisplayName |> should equal "x"

        session.EvalInteraction  "type C() = member x.P = 1"
        
        let value, name, symbol = evals.[1]
        name |> should equal "C"
        value.IsNone |> should equal true
        symbol.Symbol.GetType() |> should equal typeof<FSharpEntity>
        symbol.Symbol.DisplayName |> should equal "C"

        session.EvalInteraction  "module M = let x = ref 1"
        let value, name, symbol = evals.[2]
        name |> should equal "M"
        value.IsNone |> should equal true
        symbol.Symbol.GetType() |> should equal typeof<FSharpEntity>
        symbol.Symbol.DisplayName |> should equal "M"

let RunManually() = 
  ``EvalExpression test 1``() 
  ``EvalExpression test 1 nothrow``() 
  ``EvalExpression fsi test``() 
  ``EvalExpression fsi test 2``() 
  ``EvalExpression typecheck failure``() 
  ``EvalExpression typecheck failure nothrow``() 
  ``EvalExpression function value 1``() 
  ``EvalExpression function value 2``() 
  ``EvalExpression runtime failure``() 
  ``EvalExpression parse failure``() 
  ``EvalExpression parse failure nothrow``() 
  ``EvalInteraction typecheck failure``() 
  ``EvalInteraction typecheck failure nothrow``() 
  ``EvalInteraction runtime failure``() 
  ``EvalInteraction runtime failure nothrow``() 
  ``EvalInteraction parse failure``() 
  ``EvalInteraction parse failure nothrow``() 
  ``PartialAssemblySignatureUpdated test``() 
  ``ParseAndCheckInteraction test 1``() 
  ``Bad arguments to session creation 1``()
  ``Bad arguments to session creation 2``()
  ``EvalScript accepts paths verbatim``()
  ``EvalScript accepts paths verbatim nothrow``()
  ``interactive session events``()
  ``Disposing interactive session (collectible)``() 

//#endif
