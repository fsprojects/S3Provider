#### 0.0.1 - December 14 2013
* Initial release

#### 0.0.2 - December 16 2013
* Support versioned buckets and show all versions of a S3 object
* Support folder structures inside a bucket
* Support the use of generic `Search` types on buckets which filters keys by supplied prefix
* Object type names no longer has the prefix of parent folders
* For large buckets, don't load everything, but push user to use the `Search` type instead
* `S3Provider.SimpleStorageService` is renamed to `S3Provider.Account`
* Top level module `Provider` is made into a namespace and renamed to `AwsProviders`

#### 0.0.3 - December 17 2013
* Versions of a S3 object is sorted chronologically

#### 0.0.4 - December 18 2013
* Removed dependency on AWSSDK
* Fixed bug with getting data for versioned S3 object