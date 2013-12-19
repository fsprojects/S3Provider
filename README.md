S3 Type Provider
=======================

An experimental type provider for **Amazon S3**.

### Example

    // create a type representing the S3 account with the specified AWS credentials
    type S3 = S3Provider.Account<"AWS Key", "AWS Secret">
    
    // then access meta-data and content of objects in S3 with full intellisense support!

	// immediately after the type representing the account are all the buckets
    type bucket = S3.``my.bucket``

	// you can then select folders/files from that bucket
    type folder = bucket.``2013-12-13/``

	// on files, you can get meta-data such as ETag, LastModified, or fetch the content as
	//		* Raw   - raw binary array
	//		* UTF8  - the content as decoded using UTF8
	//		* ASCII - the content as decoded using ASCII
    let etag = folder.``My file.txt``.ETag
	let utf8 = folder.``My file.txt``.Content.UTF8
	let raw  = folder.``My image.png``.Content.Raw
	let lastModified = folder.``My image.png``.LastModified

	// if the bucket/folder is large, you can also use the `Search<...>` generic type to find
	// files in the bucket by prefix
	type search = bucket.Search<"2013-12-">

	// you can then navigate through the search results the same as before!
	let searchResultContent = search.``2013-12-13``.``My file.txt``.Content.Raw

	
### Demo Video

[![ScreenShot](https://raw.github.com/theburningmonk/S3Provider/develop/docs/files/img/demo_screenshot.png)](http://www.youtube.com/watch?v=LOU00RlArqg)