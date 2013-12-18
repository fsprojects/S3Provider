namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("S3Provider")>]
[<assembly: AssemblyProductAttribute("S3Provider")>]
[<assembly: AssemblyDescriptionAttribute("F# type provider for Amazon S3")>]
[<assembly: AssemblyVersionAttribute("0.0.4")>]
[<assembly: AssemblyFileVersionAttribute("0.0.4")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.4"
