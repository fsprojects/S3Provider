module Provider

open System
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices

// #region Helpers

let erasedType<'T> assemblyName rootNamespace typeName = 
    ProvidedTypeDefinition(assemblyName, rootNamespace, typeName, Some(typeof<'T>))

let runtimeType<'T> typeName = ProvidedTypeDefinition(typeName, Some typeof<'T>)

// #endregion

let thisAssembly = Assembly.GetExecutingAssembly()
let rootNamespace = "S3Provider"

let getBuckets (client : Amazon.S3.IAmazonS3) =
    let response = client.ListBuckets()
    response.Buckets

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

let getContent (client : Amazon.S3.IAmazonS3) (bucket : Amazon.S3.Model.S3Bucket) (s3Object : Amazon.S3.Model.S3Object) =
    let req = new Amazon.S3.Model.GetObjectRequest()
    req.BucketName <- bucket.BucketName
    req.Key        <- s3Object.Key
    let response  = client.GetObject(req)
    use memStream = new System.IO.MemoryStream()
    response.ResponseStream.CopyTo(memStream)    
    memStream.ToArray()

module RuntimeHelper = 
    let getDateTime str = DateTime.Parse(str)

let createTypedContent (ownerType : ProvidedTypeDefinition) client (bucket : Amazon.S3.Model.S3Bucket) (s3Object : Amazon.S3.Model.S3Object) = 
    let contents     = getContent client bucket s3Object
    let typedContent = runtimeType<string> "Content"
    typedContent.HideObjectMethods <- true

    let utf8    = System.Text.Encoding.UTF8.GetString(contents)
    let ascii   = System.Text.Encoding.ASCII.GetString(contents)
    typedContent.AddMember(ProvidedProperty("UTF8", typeof<string>, IsStatic = true, GetterCode = (fun args -> <@@ utf8 @@>)))
    typedContent.AddMember(ProvidedProperty("ASCII", typeof<string>, IsStatic = true, GetterCode = (fun args -> <@@ ascii @@>)))
    typedContent.AddMember(ProvidedProperty("Raw", typeof<byte[]>, IsStatic = true, GetterCode = (fun args -> <@@ contents @@>)))
    typedContent

let createTypedS3Object (ownerType : ProvidedTypeDefinition) client (bucket : Amazon.S3.Model.S3Bucket) (s3Object : Amazon.S3.Model.S3Object) = 
    let typedS3Object = runtimeType<obj> s3Object.Key
    typedS3Object.AddXmlDocDelayed(fun () -> sprintf "A strongly typed interface to S3 object %s which is %d bytes in size" s3Object.Key s3Object.Size)
    typedS3Object.HideObjectMethods <- true
    
    let key, etag, size, lastModified, ownerId, ownerName = 
        s3Object.Key, s3Object.ETag, s3Object.Size, s3Object.LastModified.ToString(), s3Object.Owner.Id, s3Object.Owner.DisplayName

    typedS3Object.AddMember(ProvidedProperty("ETag", typeof<string>, IsStatic = true, GetterCode = (fun args -> <@@ etag @@>)))
    typedS3Object.AddMember(ProvidedProperty("Size", typeof<int64>, IsStatic = true, GetterCode = (fun args -> <@@ size @@>)))
    typedS3Object.AddMember(ProvidedProperty("LastModified", typeof<DateTime>, IsStatic = true, GetterCode = (fun args -> <@@ RuntimeHelper.getDateTime(lastModified) @@>)))
    typedS3Object.AddMember(ProvidedProperty("OwnerId", typeof<string>, IsStatic = true, GetterCode = (fun args -> <@@ ownerId @@>)))
    typedS3Object.AddMember(ProvidedProperty("OwnerName", typeof<string>, IsStatic = true, GetterCode = (fun args -> <@@ ownerName @@>)))

    typedS3Object.AddMemberDelayed(fun () -> createTypedContent typedS3Object client bucket s3Object)

    typedS3Object

let createTypedBucket (ownerType : ProvidedTypeDefinition) client (bucket : Amazon.S3.Model.S3Bucket) =
    let typedBucket = runtimeType<obj> bucket.BucketName
    typedBucket.AddXmlDocDelayed(fun () -> sprintf "A strongly typed interface to S3 bucket %s which was created on %A" bucket.BucketName bucket.CreationDate)
    typedBucket.HideObjectMethods <- true

    typedBucket.AddMember(ProvidedProperty("CreationDate", typeof<DateTime>, IsStatic = true, GetterCode = (fun args -> <@@ bucket.CreationDate @@>)))
    typedBucket.AddMembersDelayed(fun () -> getObjects client bucket |> Seq.map (createTypedS3Object typedBucket client bucket) |> Seq.toList)

    typedBucket

let createTypedS3 () =
    let typedS3 = erasedType<obj> thisAssembly rootNamespace "SimpleStorageService"

    let typeParams = [ ProvidedStaticParameter("awsKey", typeof<string>); ProvidedStaticParameter("awsSecret", typeof<string>) ]
    let initFunction (typeName : string) (parameterValues : obj[]) =
        match parameterValues with
        | [| :? string as awsKey; :? string as awsSecret |] ->
            let typedS3Account = erasedType<obj> thisAssembly rootNamespace typeName
            typedS3Account.AddXmlDocDelayed(fun () -> sprintf "A strongly typed interface to S3 account with key [%s] and secret [%s]" awsKey awsSecret)
            typedS3Account.HideObjectMethods <- true

            let client  = Amazon.AWSClientFactory.CreateAmazonS3Client(awsKey, awsSecret, Amazon.RegionEndpoint.USEast1)

            typedS3Account.AddMembersDelayed(fun () -> getBuckets client |> Seq.map (createTypedBucket typedS3Account client) |> Seq.toList)

            typedS3Account

    typedS3.DefineStaticParameters(parameters = typeParams, instantiationFunction = initFunction)
    typedS3.HideObjectMethods <- true
    typedS3

[<TypeProvider>]
type S3Provider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces ()

    do this.AddNamespace(rootNamespace, [ createTypedS3() ])

[<assembly:TypeProviderAssembly>]
do ()