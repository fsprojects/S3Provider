S3 Type Provider
=======================

An experimental type provider for **Amazon S3**.

### Example

    // create a type representing the S3 account with the specified AWS credentials
    type S3 = S3Provider.SimpleStorageService<"AWS Key", "AWS Secret">
    
    // access meta-data and content of an objecct in S3 with full intellisense support!
    let etag = S3.``my.bucket``.``2013-12-13/My file.txt``.ETag
    let utf8 = S3.``my.bucket``.``2013-12-13/My file.txt``.Content.UTF8
    let raw  = S3.``my.bucket``.``2013-12-13/My image.png``.Content.Raw
    let lastModified = S3.``my.bucket``.``2013-12-13/My image.png``.LastModified