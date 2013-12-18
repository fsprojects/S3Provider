namespace AwsProvider.S3

open AwsProvider.S3.Model

module S3Utils =
    val listBuckets  : (AwsCredential -> ListAllMyBucketsResult)
    val isBucketVersioned : bucketName : string -> (AwsCredential -> bool)
    val listBucket   : bucketName : string -> prefix : string -> (AwsCredential -> ListBucketResult)
    val listVersions : bucketName : string -> key : string -> versionIdMarker : string option -> (AwsCredential -> ListVersionsResult)
    val getContent   : bucketName : string -> key : string -> version : string option -> (AwsCredential -> byte[])