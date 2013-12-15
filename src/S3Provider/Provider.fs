// Container module for the S3 Type Provider
//
// ## Example
//
//      type S3  = S3Provider.Account<"AWS Key", "AWS Secret">
//      let etag = S3.``my.bucket``.``2013-12-13/My file.txt``.ETag
//      let utf8 = S3.``my.bucket``.``2013-12-13/My file.txt``.Content.UTF8
//      let raw  = S3.``my.bucket``.``2013-12-13/My image.png``.Content.Raw
//      let lastModified = S3.``my.bucket``.``2013-12-13/My image.png``.LastModified
//
namespace AwsProviders

open System
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices

[<AutoOpen>]
module internal Helpers =
    let erasedType<'T> assemblyName rootNamespace typeName = 
        ProvidedTypeDefinition(assemblyName, rootNamespace, typeName, Some(typeof<'T>), HideObjectMethods = true)

    let runtimeType<'T> typeName = 
        ProvidedTypeDefinition(typeName, Some typeof<'T>, HideObjectMethods = true)

    let thisAssembly = Assembly.GetExecutingAssembly()
    let rootNamespace = "S3Provider"

    /// Returns all the S3 buckets in the account
    let getBuckets (client : Amazon.S3.IAmazonS3) =
        let response = client.ListBuckets()
        response.Buckets

    /// Returns whether or not the specified bucket has had versioning enabled at some point
    let isBucketVersioned (client : Amazon.S3.IAmazonS3) (bucket : Amazon.S3.Model.S3Bucket) =
        let req      = new Amazon.S3.Model.GetBucketVersioningRequest()
        req.BucketName <- bucket.BucketName
        let response = client.GetBucketVersioning(req)
        match response.VersioningConfig.Status.Value with
        | "Enabled" | "Suspended" -> true
        | _ -> false

    /// Returns all the S3 objects in the specified S3 bucket
    let getObjects (client : Amazon.S3.IAmazonS3) (bucket : Amazon.S3.Model.S3Bucket) =
        let rec loop marker = seq {
            let req = new Amazon.S3.Model.ListObjectsRequest()
            req.BucketName <- bucket.BucketName
            if not <| String.IsNullOrWhiteSpace marker then req.Marker <- marker

            let response = client.ListObjects(req)
            yield! response.S3Objects
        
            if not <| String.IsNullOrWhiteSpace response.NextMarker then yield! loop response.NextMarker
        }

        loop ""

    /// Returns all the versions for the specified S3 object
    let getObjectVersions (client : Amazon.S3.IAmazonS3) (bucket : Amazon.S3.Model.S3Bucket) (s3Object : Amazon.S3.Model.S3Object) =
        let rec loop marker = seq {
            let req = new Amazon.S3.Model.ListVersionsRequest()
            req.BucketName <- bucket.BucketName
            req.Prefix     <- s3Object.Key
            if not <| String.IsNullOrWhiteSpace marker then req.VersionIdMarker <- marker

            let response = client.ListVersions(req)
            yield! response.Versions

            if not <| String.IsNullOrWhiteSpace response.NextVersionIdMarker then yield! loop response.NextVersionIdMarker
        }

        loop ""

    /// Returns the content (byte[]) of a S3 obejct
    let getContent (client : Amazon.S3.IAmazonS3) (bucket : Amazon.S3.Model.S3Bucket) version (s3Object : Amazon.S3.Model.S3Object) =
        let req = new Amazon.S3.Model.GetObjectRequest()
        req.BucketName <- bucket.BucketName
        req.Key        <- s3Object.Key
        match version with
        | Some versionId -> req.VersionId <- versionId
        | _ -> ()

        let response  = client.GetObject(req)
        use memStream = new System.IO.MemoryStream()
        response.ResponseStream.CopyTo(memStream)
        memStream.ToArray()

    module RuntimeHelper = 
        let getDateTime str = DateTime.Parse(str)
        let getUtf8String   = System.Text.Encoding.UTF8.GetString
        let getAsciiString  = System.Text.Encoding.ASCII.GetString

    /// Create a nested type to represent the content of a S3 object
    let createTypedContent (ownerType : ProvidedTypeDefinition) client (bucket : Amazon.S3.Model.S3Bucket) version (s3Object : Amazon.S3.Model.S3Object) = 
        let contents     = getContent client bucket version s3Object
        let typedContent = runtimeType<obj> "Content"

        typedContent.AddMemberDelayed(fun () -> ProvidedProperty("UTF8", typeof<string>, IsStatic = true, GetterCode = (fun args -> <@@ RuntimeHelper.getUtf8String(contents) @@>)))
        typedContent.AddMemberDelayed(fun () -> ProvidedProperty("ASCII", typeof<string>, IsStatic = true, GetterCode = (fun args -> <@@ RuntimeHelper.getAsciiString(contents) @@>)))
        typedContent.AddMember(ProvidedProperty("Raw", typeof<byte[]>, IsStatic = true, GetterCode = (fun args -> <@@ contents @@>)))
        typedContent

    /// Create a nested type to represent a non-versioned S3 object
    let createTypedS3Object (ownerType : ProvidedTypeDefinition) client (bucket : Amazon.S3.Model.S3Bucket) typeName (s3Object : Amazon.S3.Model.S3Object) = 
        let typeName = 
            match typeName, s3Object with 
            | Some name, _ -> name 
            | _, (:? Amazon.S3.Model.S3ObjectVersion as s3ObjectVersion)
                -> if s3ObjectVersion.IsLatest 
                   then sprintf "%s (Latest, %A)" s3ObjectVersion.VersionId s3ObjectVersion.LastModified
                   else sprintf "%s (%A)" s3ObjectVersion.VersionId s3ObjectVersion.LastModified
            | _ -> s3Object.Key
        let typedS3Object = runtimeType<obj> typeName
        typedS3Object.AddXmlDocDelayed(fun () -> sprintf "A strongly typed interface to S3 object %s which is %d bytes in size" s3Object.Key s3Object.Size)
    
        let key, etag, size, lastModified, ownerId, ownerName = 
            s3Object.Key, s3Object.ETag, s3Object.Size, s3Object.LastModified.ToString(), s3Object.Owner.Id, s3Object.Owner.DisplayName

        typedS3Object.AddMember(ProvidedProperty("ETag", typeof<string>, IsStatic = true, GetterCode = (fun args -> <@@ etag @@>)))
        typedS3Object.AddMember(ProvidedProperty("Size", typeof<int64>, IsStatic = true, GetterCode = (fun args -> <@@ size @@>)))
        typedS3Object.AddMember(ProvidedProperty("LastModified", typeof<DateTime>, IsStatic = true, GetterCode = (fun args -> <@@ RuntimeHelper.getDateTime(lastModified) @@>)))
        typedS3Object.AddMember(ProvidedProperty("OwnerId", typeof<string>, IsStatic = true, GetterCode = (fun args -> <@@ ownerId @@>)))
        typedS3Object.AddMember(ProvidedProperty("OwnerName", typeof<string>, IsStatic = true, GetterCode = (fun args -> <@@ ownerName @@>)))
        typedS3Object.AddMemberDelayed(fun () -> createTypedContent typedS3Object client bucket None s3Object)

        typedS3Object

    /// Create a nested type to represent a versioned S3 object
    let createTypedVersionedS3Object (ownerType : ProvidedTypeDefinition) client (bucket : Amazon.S3.Model.S3Bucket) (s3Object : Amazon.S3.Model.S3Object) =
        let typedS3Object = runtimeType<obj> s3Object.Key
        typedS3Object.AddXmlDocDelayed(fun () -> sprintf "A strongly typed interface to a versioned S3 object %s" s3Object.Key)

        // nested type to represent the latest version of the object
        typedS3Object.AddMember(createTypedS3Object ownerType client bucket (Some "Latest") s3Object)

        // then a nested type called Versions to represent all the versions of the object
        let typedS3ObjectVersions = runtimeType<obj> "Versions"
        typedS3Object.AddMember(typedS3ObjectVersions)
        typedS3ObjectVersions.AddMembersDelayed(fun () ->
            let versions = getObjectVersions client bucket s3Object
            versions |> Seq.map (createTypedS3Object ownerType client bucket None) |> Seq.toList)

        typedS3Object

    /// Create a nested type to represent a S3 bucket
    let createTypedBucket (ownerType : ProvidedTypeDefinition) client (bucket : Amazon.S3.Model.S3Bucket) =
        let typedBucket = runtimeType<obj> bucket.BucketName
        let isVersioned = isBucketVersioned client bucket
        typedBucket.AddXmlDocDelayed(fun () -> sprintf "A strongly typed interface to S3 bucket %s which was created on %A, and %s versioned" 
                                                       bucket.BucketName bucket.CreationDate (if isVersioned then "is" else "is not"))

        typedBucket.AddMember(ProvidedProperty("CreationDate", typeof<DateTime>, IsStatic = true, GetterCode = (fun args -> <@@ bucket.CreationDate @@>)))

        let createTypedS3Object = 
            match isVersioned with
            | false -> createTypedS3Object typedBucket client bucket None
            | true  -> createTypedVersionedS3Object typedBucket client bucket

        typedBucket.AddMembersDelayed(fun () -> getObjects client bucket |> Seq.map createTypedS3Object |> Seq.toList)

        typedBucket

    /// Create an erased type to represent a S3 account
    let createTypedAccount () =
        let typedS3 = erasedType<obj> thisAssembly rootNamespace "Account"

        let typeParams = [ ProvidedStaticParameter("awsKey", typeof<string>); ProvidedStaticParameter("awsSecret", typeof<string>) ]
        let initFunction (typeName : string) (parameterValues : obj[]) =
            match parameterValues with
            | [| :? string as awsKey; :? string as awsSecret |] ->
                let typedS3Account = erasedType<obj> thisAssembly rootNamespace typeName
                typedS3Account.AddXmlDocDelayed(fun () -> sprintf "A strongly typed interface to S3 account with key [%s] and secret [%s]" awsKey awsSecret)

                let client  = Amazon.AWSClientFactory.CreateAmazonS3Client(awsKey, awsSecret, Amazon.RegionEndpoint.USEast1)

                typedS3Account.AddMembersDelayed(fun () -> getBuckets client |> Seq.map (createTypedBucket typedS3Account client) |> Seq.toList)

                typedS3Account

        typedS3.DefineStaticParameters(parameters = typeParams, instantiationFunction = initFunction)
        typedS3

[<TypeProvider>]
type S3Provider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces ()

    do this.AddNamespace(rootNamespace, [ createTypedAccount() ])

[<assembly:TypeProviderAssembly>]
do ()