namespace AwsProvider.S3.Model

open System
open System.Xml.Serialization

type AwsCredential = { AwsKey : string; AwsSecret : string }

[<CLIMutable>]
[<XmlRoot("Owner", Namespace = "http://s3.amazonaws.com/doc/2006-03-01/")>]
type Owner =
    {
        [<XmlElement("ID")>] 
        Id          : string
        [<XmlElement("DisplayName")>]
        DisplayName : string
    }

[<CLIMutable>]
[<XmlRoot("Bucket", Namespace = "http://s3.amazonaws.com/doc/2006-03-01/")>]
type Bucket = 
    {
        [<XmlElement("Name")>]
        Name         : string
        [<XmlElement("CreationDate")>] 
        CreationDate : DateTime
    }

[<CLIMutable>]
[<XmlRoot("CommonPrefixes", Namespace = "http://s3.amazonaws.com/doc/2006-03-01/")>]
type CommonPrefixes = 
    {
        [<XmlElement("Prefix")>]
        Prefix  : string
    }

type IS3Object =
    abstract member Key          : string
    abstract member LastModified : DateTime
    abstract member ETag         : string
    abstract member Size         : uint64
    abstract member Owner        : Owner
    abstract member StorageClass : string

type IS3ObjectVersion =
    inherit IS3Object
    abstract member VersionId    : string
    abstract member IsLatest     : bool

[<CLIMutable>]
[<XmlRoot("Contents", Namespace = "http://s3.amazonaws.com/doc/2006-03-01/")>]
type S3Object = 
    {
        [<XmlElement("Key")>]
        Key          : string
        [<XmlElement("LastModified")>]
        LastModified : DateTime
        [<XmlElement("ETag")>]
        ETag         : string
        [<XmlElement("Size")>]
        Size         : uint64
        [<XmlElement("Owner")>]
        Owner        : Owner
        [<XmlElement("StorageClass")>]
        StorageClass : string
    }

    interface IS3Object with
        member this.Key          = this.Key
        member this.LastModified = this.LastModified
        member this.ETag         = this.ETag
        member this.Size         = this.Size
        member this.Owner        = this.Owner
        member this.StorageClass = this.StorageClass

[<CLIMutable>]
[<XmlRoot("Version", Namespace = "http://s3.amazonaws.com/doc/2006-03-01/")>]
type S3ObjectVersion = 
    {
        [<XmlElement("Key")>]
        Key          : string
        [<XmlElement("VersionId")>]
        VersionId    : string
        [<XmlElement("IsLatest")>]
        IsLatest     : bool
        [<XmlElement("LastModified")>]
        LastModified : DateTime
        [<XmlElement("ETag")>]
        ETag         : string
        [<XmlElement("Size")>]
        Size         : uint64
        [<XmlElement("Owner")>]
        Owner        : Owner
        [<XmlElement("StorageClass")>]
        StorageClass : string
    }

    interface IS3ObjectVersion with
        member this.Key          = this.Key
        member this.LastModified = this.LastModified
        member this.ETag         = this.ETag
        member this.Size         = this.Size
        member this.Owner        = this.Owner
        member this.StorageClass = this.StorageClass
        member this.VersionId    = this.VersionId
        member this.IsLatest     = this.IsLatest

[<CLIMutable>]
[<XmlRoot("ListAllMyBucketsResult", Namespace = "http://s3.amazonaws.com/doc/2006-03-01/")>]
type ListAllMyBucketsResult =
    {
        [<XmlElement("Owner")>]
        Owner   : Owner
        [<XmlArray("Buckets")>]
        [<XmlArrayItem(typeof<Bucket>, ElementName = "Bucket")>]
        Buckets : Bucket[]
    }

[<CLIMutable>]
[<XmlRoot("VersioningConfiguration", Namespace = "http://s3.amazonaws.com/doc/2006-03-01/")>]
type VersioningConfiguration =
    {
        [<XmlElement("Status")>]
        Status    : string
        [<XmlElement("MfaDelete")>]
        MfaDelete : string
    }
    
[<CLIMutable>]
[<XmlRoot("ListBucketResult", Namespace = "http://s3.amazonaws.com/doc/2006-03-01/")>]
type ListBucketResult =
    {
        [<XmlElement("Name")>]
        Name            : string
        [<XmlElement("Prefix")>]
        Prefix          : string
        [<XmlElement("Marker")>]
        Marker          : string
        [<XmlElement("NextMarker")>]
        NextMarker      : string
        [<XmlElement("MaxKeys")>]
        MaxKeys         : int
        [<XmlElement("Delimiter")>]
        Delimiter       : string
        [<XmlElement("IsTruncated")>]
        IsTruncated     : bool
        [<XmlElement("Contents")>]
        S3Objects       : S3Object[]
        [<XmlElement("CommonPrefixes")>]
        CommonPrefixes  : CommonPrefixes[]
    }

[<CLIMutable>]
[<XmlRoot("ListVersionsResult", Namespace = "http://s3.amazonaws.com/doc/2006-03-01/")>]
type ListVersionsResult =
    {
        [<XmlElement("Name")>]
        Name            : string
        [<XmlElement("Prefix")>]
        Prefix          : string
        [<XmlElement("Marker")>]
        Marker          : string
        [<XmlElement("VersionIdMarker")>]
        VersionIdMarker : string
        [<XmlElement("NextKeyMarker")>]
        NextKeyMarker   : string
        [<XmlElement("NextVersionIdMarker")>]
        NextVersionIdMarker : string
        [<XmlElement("MaxKeys")>]
        MaxKeys         : int
        [<XmlElement("Delimiter")>]
        Delimiter       : string
        [<XmlElement("IsTruncated")>]
        IsTruncated     : bool
        [<XmlElement("Version")>]
        Versions        : S3ObjectVersion[]
    }