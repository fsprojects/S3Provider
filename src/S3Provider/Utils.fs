namespace AwsProvider.S3

open System
open System.Globalization
open System.IO
open System.Linq
open System.Net
open System.Xml.Serialization
open System.Security.Cryptography
open System.Text

open AwsProvider.S3.Model

/// To find out more on REST authentication with S3, please read:
///     http://docs.aws.amazon.com/AmazonS3/latest/dev/RESTAuthentication.html
/// the rest of the code assumes a path-style request, i.e. instead of
///     https://johnsmith.s3.amazonaws.com/photos/puppy.jpg
/// the URI used will be
///     https://s3.amazonaws.com/johnsmith/photos/puppy.jpg
 
 module S3Utils = 
    let baseUri   = Uri "http://s3.amazonaws.com"
    let userAgent = "S3Provider (https://github.com/theburningmonk/S3Provider/)"
    let subresources = [| "acl"; "cors"; "delete"; "lifecycle"; "location"; "logging"; "notification"; "partNumber"; "policy"; "requestPayment"; "restore"; "tagging"; "torrent"; "uploadId"; "uploads"; "versionId"; "versioning"; "versions"; "website" |]
                       |> Set.ofArray

    let getTimestampRFC822 (date : DateTime) =
        date.ToString("ddd, dd MMM yyyy HH:mm:ss \\G\\M\\T", CultureInfo.InvariantCulture)

    let getCannonicalResource (relativePath : string) =
        let path, subresources = 
            match relativePath.IndexOf('?') with
            | -1  -> relativePath, [||]
            | idx -> 
                let path = relativePath.Substring(0, idx)
                let subresources =
                    relativePath.Substring(idx + 1).Split([| '&'; ';' |], StringSplitOptions.RemoveEmptyEntries)
                    |> Array.map (fun str -> str.Split('='))
                    // only keep resources that have null values (e.g. acl, versions) or have non-empty values
                    |> Array.filter (function | [| param |] when subresources.Contains param -> true
                                                | [| param; value |] 
                                                when subresources.Contains param && value <> "" -> true
                                                | _                  -> false)
                path, subresources
        
        match subresources with
        | [||] -> path
        | subresources ->
            subresources
                // null-value resources (e.g. acl, versions) come first
                .OrderBy(fun arr -> arr.Length)
                // other resources are ordered alphabeticaly
                .ThenBy(fun arr -> arr.[0])
            |> Seq.map (function | [| param |] -> param | [| param; value |] -> sprintf "%s=%s" param value)
            |> fun seq -> String.Join("&", seq)
            |> fun sub -> sprintf "%s?%s" path sub

    let getCannonicalAmzHeaders (headers : WebHeaderCollection) =
        headers.AllKeys
        |> Seq.filter (fun key -> key.ToLower().StartsWith("x-amz"))
        |> Seq.sort
        |> Seq.map (fun key -> sprintf "%s:%s" key headers.[key])
        |> Seq.map (fun str -> str.Trim())
        |> fun lst -> String.Join("\n", lst)
    
    let getStringToSign relativePath headers date =
        // no Content-MD5 and Content-Type headers since we're doing a GET
        "GET\n\n\n" + date + "\n" + getCannonicalAmzHeaders headers + getCannonicalResource relativePath

    let getSignature { AwsSecret = secret } (payload : string) =
        let data = Encoding.UTF8.GetBytes(payload)
        let algo = KeyedHashAlgorithm.Create("HMACSHA1")
        algo.Key <- Encoding.UTF8.GetBytes(secret)
        algo.ComputeHash(data) |> Convert.ToBase64String
       
    /// Creates a HttpWebRequest for the specified relative path
    let createRequest (relativePath : string) awsCred = 
        let uri = Uri(baseUri, relativePath)
        let req = WebRequest.Create(uri) :?> HttpWebRequest
        req.Timeout                   <- Int32.MaxValue
        req.ReadWriteTimeout          <- Int32.MaxValue
        req.AllowWriteStreamBuffering <- false

        req.Date <- DateTime.UtcNow
        req.UserAgent <- userAgent
        let dateString = getTimestampRFC822 req.Date
        let stringToSign = getStringToSign relativePath req.Headers dateString
        printfn "%s" stringToSign

        let signature = stringToSign |> getSignature awsCred
        let authorization = sprintf "AWS %s:%s" awsCred.AwsKey signature
        req.Headers.Add("Authorization", authorization)

        req

    let executeRequest<'T> (req : HttpWebRequest) =
        let response = req.GetResponse()
        use stream   = response.GetResponseStream()
        let serializer = new XmlSerializer(typeof<'T>)
        serializer.Deserialize(stream) :?> 'T

    let listBuckets = createRequest "/" >> executeRequest<ListAllMyBucketsResult>

    let isBucketVersioned bucketName = 
        sprintf "/%s/?versioning" bucketName
        |> createRequest
        >> executeRequest<VersioningConfiguration>
        >> fun config -> match config.Status with 
                         | "Enabled" | "enabled" 
                         | "Suspended" | "suspended" -> true 
                         | _ -> false

    let listBucket bucketName prefix =
        sprintf "/%s/?prefix=%s&max-keys=1000&delimiter=/" bucketName prefix
        |> createRequest
        >> executeRequest<ListBucketResult>

    let listVersions bucketName key versionIdMarker =
        let relPath = 
            match versionIdMarker with
            | Some marker -> sprintf "/%s/?versions&prefix=%s&version-id-marker=%s&key-marker=%s&max-keys=1000&delimiter=/" bucketName key marker key
            | _ -> sprintf "/%s/?versions&prefix=%s&max-keys=1000&delimiter=/" bucketName key

        relPath
        |> createRequest
        >> executeRequest<ListVersionsResult>

    let getContent bucketName key version =        
        let relPath = match version with
                      | Some versionId -> sprintf "/%s/%s?versionId=%s" bucketName key versionId
                      | _ -> sprintf "/%s/%s" bucketName key

        createRequest relPath
        >> (fun req -> 
                use responseStream = req.GetResponse().GetResponseStream()
                use memStream = new MemoryStream()
                responseStream.CopyTo(memStream)
                memStream.ToArray())