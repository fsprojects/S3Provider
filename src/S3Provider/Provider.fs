// Container module for the S3 Type Provider
//
// ## Example
//
//      type S3  = S3Provider.Account<"AWS Key", "AWS Secret">
//      type bucket = S3.``my.bucket``
//      type folder = bucket.``2013-12-13/``
//      let etag = folder.``My file.txt``.ETag
//      let utf8 = folder.``My file.txt``.Content.UTF8
//      let raw  = folder.``My image.png``.Content.Raw
//      let lastModified = folder.``My image.png``.LastModified
//      type search = bucket.Search<"2013-12-">
//      let searchResultContent = search.``2013-12-13``.``My file.txt``.Content.Raw
//
namespace AwsProviders

open System
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices

open AwsProvider.S3
open AwsProvider.S3.Model

module RuntimeHelper = 
    let getDateTime str = DateTime.Parse(str)
    let getUtf8String   = System.Text.Encoding.UTF8.GetString
    let getAsciiString  = System.Text.Encoding.ASCII.GetString

[<AutoOpen>]
module internal Helpers =
    type Prefix     = Prefix     of string
    type FolderName = FolderName of string

    type S3Entry =
        | Folder    of Prefix * FolderName
        | S3Object  of IS3Object

    let erasedType<'T> assemblyName rootNamespace typeName = 
        ProvidedTypeDefinition(assemblyName, rootNamespace, typeName, Some(typeof<'T>), HideObjectMethods = true)

    let runtimeType<'T> typeName = 
        ProvidedTypeDefinition(typeName, Some typeof<'T>, HideObjectMethods = true)

    let thisAssembly = Assembly.GetExecutingAssembly()
    let rootNamespace = "S3Provider"

    /// Returns all the S3 buckets in the account
    let getBuckets awsCred =
        let result = S3Utils.listBuckets awsCred
        result.Buckets

    /// Returns whether or not the specified bucket has had versioning enabled at some point
    let isBucketVersioned awsCred (bucket : Bucket) = S3Utils.isBucketVersioned bucket.Name awsCred

    /// Returns a pretty name (without the folder prefixes) for the specified S3 object
    let getPrettyS3ObjectName (s3Object : IS3Object) =
        match s3Object.Key.LastIndexOf("/") with
        | -1  -> s3Object.Key
        | idx -> s3Object.Key.Substring(idx + 1)

    /// Returns a pretty name (without the folder prefixes) for the specified S3 object
    let getPrettyFolderName (Prefix prefix) =
        match prefix.LastIndexOf('/', prefix.Length - 2, prefix.Length - 1) with
        | -1  -> prefix
        | idx -> prefix.Substring(idx + 1)

    /// Returns all the S3 objects in the specified S3 bucket
    let getObjects awsCred (bucket : Bucket) (Prefix prefix) =
        let result = S3Utils.listBucket bucket.Name prefix awsCred

        let entries = 
            [|
                if result.CommonPrefixes <> null then
                    yield! result.CommonPrefixes 
                           |> Seq.map (fun prefixes -> 
                                let prefix = Prefix prefixes.Prefix
                                Folder(prefix, FolderName <| getPrettyFolderName prefix))

                if result.S3Objects <> null then
                    yield! result.S3Objects |> Seq.map (fun s3Obj -> s3Obj :> IS3Object |> S3Object)
            |]

        // return whether or not there's more results, as well as the current set of results
        result.IsTruncated, entries

    /// Returns all the versions for the specified S3 object
    let getObjectVersions awsCred (bucket : Bucket) (s3Object : IS3Object) =
        let rec loop marker = seq {
            let result = S3Utils.listVersions bucket.Name s3Object.Key marker awsCred
            yield! result.Versions

            if not <| String.IsNullOrWhiteSpace result.NextVersionIdMarker then yield! loop (Some result.NextVersionIdMarker)
        }

        loop None |> Seq.toArray

    /// Returns the content (byte[]) of a S3 obejct
    let getContent awsCred (bucket : Bucket) (s3Object : IS3Object) =
        let version = match s3Object with
                      | :? IS3ObjectVersion as objVersion -> Some objVersion.VersionId
                      | _ -> None

        S3Utils.getContent bucket.Name s3Object.Key version awsCred

    /// Create a nested type to represent the content of a S3 object
    let createTypedContent (ownerType : ProvidedTypeDefinition) awsCred (bucket : Bucket) (s3Object : IS3Object) = 
        let contents     = getContent awsCred bucket s3Object
        let typedContent = runtimeType<obj> "Content"

        typedContent.AddMemberDelayed(fun () -> ProvidedProperty("UTF8", typeof<string>, IsStatic = true, GetterCode = (fun args -> <@@ RuntimeHelper.getUtf8String(contents) @@>)))
        typedContent.AddMemberDelayed(fun () -> ProvidedProperty("ASCII", typeof<string>, IsStatic = true, GetterCode = (fun args -> <@@ RuntimeHelper.getAsciiString(contents) @@>)))
        typedContent.AddMember(ProvidedProperty("Raw", typeof<byte[]>, IsStatic = true, GetterCode = (fun args -> <@@ contents @@>)))
        typedContent

    /// Create a nested type to represent a non-versioned S3 object
    let createTypedS3Object (ownerType : ProvidedTypeDefinition) awsCred (bucket : Bucket) typeName (s3Object : IS3Object) = 
        let typeName = 
            match typeName, s3Object with 
            | Some name, _ -> name 
            | _, (:? IS3ObjectVersion as s3ObjectVersion)
                -> if s3ObjectVersion.IsLatest 
                   then sprintf "(%s, Latest) %s" (s3ObjectVersion.LastModified.ToString("yyyy-MM-dd HH:mm:ss")) s3ObjectVersion.VersionId
                   else sprintf "(%s) %s" (s3ObjectVersion.LastModified.ToString("yyyy-MM-dd HH:mm:ss")) s3ObjectVersion.VersionId
            | _ -> getPrettyS3ObjectName s3Object
        let typedS3Object = runtimeType<obj> typeName
        typedS3Object.AddXmlDoc(sprintf "A strongly typed interface to S3 object %s which is %d bytes in size" s3Object.Key s3Object.Size)
    
        let key, etag, size, lastModified, ownerId, ownerName = 
            s3Object.Key, s3Object.ETag, s3Object.Size, s3Object.LastModified.ToString(), s3Object.Owner.Id, s3Object.Owner.DisplayName

        typedS3Object.AddMember(ProvidedProperty("ETag", typeof<string>, IsStatic = true, GetterCode = (fun args -> <@@ etag @@>)))
        typedS3Object.AddMember(ProvidedProperty("Size", typeof<int64>, IsStatic = true, GetterCode = (fun args -> <@@ size @@>)))
        typedS3Object.AddMember(ProvidedProperty("LastModified", typeof<DateTime>, IsStatic = true, GetterCode = (fun args -> <@@ RuntimeHelper.getDateTime(lastModified) @@>)))
        typedS3Object.AddMember(ProvidedProperty("OwnerId", typeof<string>, IsStatic = true, GetterCode = (fun args -> <@@ ownerId @@>)))
        typedS3Object.AddMember(ProvidedProperty("OwnerName", typeof<string>, IsStatic = true, GetterCode = (fun args -> <@@ ownerName @@>)))
        typedS3Object.AddMemberDelayed(fun () -> createTypedContent typedS3Object awsCred bucket s3Object)

        typedS3Object

    /// Create a nested type to represent a versioned S3 object
    let createTypedVersionedS3Object (ownerType : ProvidedTypeDefinition) awsCred (bucket : Bucket) (s3Object : IS3Object) =
        let typedS3Object = getPrettyS3ObjectName s3Object |> runtimeType<obj>
        typedS3Object.AddXmlDoc(sprintf "A strongly typed interface to a versioned S3 object %s" s3Object.Key)

        // nested type to represent the latest version of the object
        typedS3Object.AddMember(createTypedS3Object ownerType awsCred bucket (Some "Latest") s3Object)

        // then a nested type called Versions to represent all the versions of the object
        let typedS3ObjectVersions = runtimeType<obj> "Versions"
        typedS3Object.AddMember(typedS3ObjectVersions)
        typedS3ObjectVersions.AddMembersDelayed(fun () ->
            let versions = getObjectVersions awsCred bucket s3Object
            versions |> Seq.map (createTypedS3Object ownerType awsCred bucket None) |> Seq.toList)

        typedS3Object

    /// Create a nested type to represent a S3 folder
    let rec createTypedS3Folder (ownerType : ProvidedTypeDefinition) awsCred (bucket : Bucket) isVersioned prefix folderName =
        let typedFolder = runtimeType<obj> folderName
        typedFolder.AddXmlDoc(sprintf "A strongly typed interface to a S3 folder %s" prefix)

        typedFolder.AddMembersDelayed(fun () -> createTypedS3Entries typedFolder awsCred bucket isVersioned prefix)

        typedFolder

    /// Create a nested type to represent a S3 entry (either Folder or S3Object)
    and createTypedS3Entry (ownerType : ProvidedTypeDefinition) awsCred bucket isVersioned = function
        | Folder(Prefix prefix, FolderName folderName) -> createTypedS3Folder ownerType awsCred bucket isVersioned prefix folderName
        | S3Object(s3Object) when isVersioned -> createTypedVersionedS3Object ownerType awsCred bucket s3Object
        | S3Object(s3Object) -> createTypedS3Object ownerType awsCred bucket None s3Object

    /// Create and return the list of nested types representing entries that are returned by the given prefix
    and createTypedS3Entries (ownerType : ProvidedTypeDefinition) awsCred bucket isVersioned prefix =
        let (isTruncated, entries) = getObjects awsCred bucket (Prefix prefix)

        seq {
            yield! entries |> Seq.map (createTypedS3Entry ownerType awsCred bucket isVersioned)
            if isTruncated then yield runtimeType<obj> "Too many results, use Search<...> instead"
        }
        |> Seq.toList

    /// Create a nested parametric type to represent a Search
    let createTypedSearch (ownerType : ProvidedTypeDefinition) awsCred (bucket : Bucket) isVersioned =
        let genericSearch = runtimeType<obj> "Search"
        
        let typeParams = [ ProvidedStaticParameter("prefix", typeof<string>) ]
        let initFunction typeName (parameterValues : obj[]) =
            match parameterValues with
            | [| :? string as prefix |] ->
                let typedSearch = runtimeType<obj> typeName
                typedSearch.AddXmlDoc(sprintf "A strongly typed interface to a S3 search with prefix [%s]" prefix)
                typedSearch.AddMembersDelayed(fun () -> createTypedS3Entries typedSearch awsCred bucket isVersioned prefix)

                ownerType.AddMember(typedSearch)
                typedSearch

        genericSearch.DefineStaticParameters(parameters = typeParams, instantiationFunction = initFunction)
        genericSearch

    /// Create a nested type to represent a S3 bucket
    let createTypedBucket (ownerType : ProvidedTypeDefinition) awsCred (bucket : Bucket) =
        let typedBucket = runtimeType<obj> bucket.Name
        typedBucket.AddXmlDoc(sprintf "A strongly typed interface to S3 bucket %s which was created on %A" 
                                      bucket.Name bucket.CreationDate)

        typedBucket.AddMember(ProvidedProperty("CreationDate", typeof<DateTime>, IsStatic = true, GetterCode = (fun args -> <@@ bucket.CreationDate @@>)))
        typedBucket.AddMembersDelayed(fun () -> 
            let isVersioned = isBucketVersioned awsCred bucket
            
            let typedSearch = createTypedSearch typedBucket awsCred bucket isVersioned
            let typedEntries = createTypedS3Entries typedBucket awsCred bucket isVersioned "" // use empty string as prefix for the top level bucket
                
            typedSearch :: typedEntries)

        typedBucket

    /// Create an erased type to represent a S3 account
    let createTypedAccount () =
        let typedS3 = erasedType<obj> thisAssembly rootNamespace "Account"

        let typeParams = [ ProvidedStaticParameter("awsKey", typeof<string>); ProvidedStaticParameter("awsSecret", typeof<string>) ]
        let initFunction (typeName : string) (parameterValues : obj[]) =
            match parameterValues with
            | [| :? string as awsKey; :? string as awsSecret |] ->
                let typedS3Account = erasedType<obj> thisAssembly rootNamespace typeName
                typedS3Account.AddXmlDoc(sprintf "A strongly typed interface to S3 account with key [%s] and secret [%s]" awsKey awsSecret)

                let awsCred = { AwsKey = awsKey; AwsSecret = awsSecret }

                typedS3Account.AddMembersDelayed(fun () -> getBuckets awsCred |> Seq.map (createTypedBucket typedS3Account awsCred) |> Seq.toList)

                typedS3Account

        typedS3.DefineStaticParameters(parameters = typeParams, instantiationFunction = initFunction)
        typedS3

[<TypeProvider>]
type S3Provider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces ()

    do this.AddNamespace(rootNamespace, [ createTypedAccount() ])

[<assembly:TypeProviderAssembly>]
do ()