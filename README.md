# Overview
SolidSharp implements  a simple [Solid Compression file](https://en.wikipedia.org/wiki/Solid_compression) in pure C# with support for extensible tagging and metadata.

## Use Cases
SolidSharp is well suited for use cases where a large collection of documents needs to be delivered as a single file to a client, and where the cost of decompression and representing these as discrete files on disk is prohibitively costly - such as an [Android mobile phone running FUSE](http://fixbugfix.blogspot.com/2015/11/why-does-fuse-on-android-suck.html).  It's also generally only suitable when a fraction of this content is interacted with at a time.

This was developed as a bespoke file format for delivering the latest content meta-data for a portfolio of mobile games. Ultimately however, this is a fairly narrow use case and should only be considered if you need random access to a large body of documents with low upfront processing overhead and predictable access times.

## Features

 - **Minimal Initial Processing Overhead** - Unlike ZIP, SolidSharp front-loads all the metadata about the archive. This means fewer bytes need to be processed before the archive is ready to be used.
 - **Content Tagging** - Documents can be added to a SolidSharp archive with tags. At runtime, this data can be be used as an index to query and grab certain subsections of the content library - for instance. Find me all documents that are tagged with "level" or "powerup".
 - **Extensible Metadata** - Developers can inline arbitrary global metadata or per-document metadata. The metadata is fetched as part of the initial upfront processing and can be used to enriched the content further if needed.


## File Format Overview
A SolidSharpFile is relatively straight forward. Indexing and content metadata are front loaded and parsed into memory upfront and remaining resident until the SharpFile is disposed.



## Usage

### Basic Creation

    using (SolidFile sf = new SolidFile())  
    {  
        sf.AddFile("solid_file_test_01.json.txt");  
        sf.Write("test.ssf");  
    }
Entries can be created from files or from raw byte[]. You can write out the file to an absolute filename or to an arbitrary stream. Writing a SolidFile products a basic diagnostic report that you can inspect and includes details on relative sizing of the entries as well as tag usage (if any)

### Deserialization

    SolidFile sf = new SolidFile("test.ssf");  
    var contents = sf.Get("solid_file_test_01").Contents;  
    Console.WriteLine(Encoding.UTF8.GetString(contents));

You can load up a SolidFile from either an absolute path on disk, or an arbitrary stream

SolidFile implements the IDisposable interface - however, for most use cases its recommended to keep the archive open (indefinitely) as this amortizes the initialization costs across multiple uses.

### Tagging
Entries are tagged as they're added to the archive

    using (SolidFile sf = new SolidFile())  
    {  
        sf.AddFile("solid_file_test_01.json.txt", "solid_file_test_01",  new List<string> {"A", "B", "C"});  
        sf.AddFile("solid_file_test_02.json.txt", "solid_file_test_02", new List<string> {"B", "C", "D"});  
        sf.Write("test.ssf");  
    }

As part of writing the archive, these are deduped into an integer based dictionary to drop some size off the resulting archive. These can be subsequently queried from a loaded archive by calling the GetByTag or GetMetaByTag APIs

    SolidFile sf = new SolidFile("test.ssf");  
    var contents = sf.Get("solid_file_test_01").Contents;  
    Console.WriteLine("Got " + read.GetByTag("A").Count + " by tag A");  // 1 entries
	Console.WriteLine("Got " + read.GetByTag("B").Count + " by tag B");  // 2 entries

### Metadata

You can inline an arbitrary byte[] as global meta data simply by calling AttachGlobalMetaData

    SolidFile sf = new SolidFile();  
    sf.AttachGlobalMeta(Encoding.UTF8.GetBytes("Be sure to drink your ovaltine"));
    
And fetch it out later

    Console.WriteLine("Got meta " + Encoding.UTF8.GetString(sf.GlobalMeta)); // Be sure to drink your ovaltine

Similarly, you can attach a byte[] to individual entries as they're added to the archive. 

*Important - Keep in mind that the semantics of metadata are that they are loaded and resident in memory until the archive is disposed.* 


## FAQ

 *1. What are the tradeoffs?*
 Generally speaking, you should aim to serve up your SolidFile as uncompressed. This is generally because you're selecting this format for a use case where decompression costs are prohibitively costly. However, this will mean a larger file size, both over the wire and on disk.
 
 *2. This is very similar to a WAD - Why didn't you just go with that?*
 While WADs do implement a Solid archive - it's somewhat of a poorly supported formated in C#. Additionally, I wanted to toss on some arbitrary metadata and tag-based indexing.
 
 *3. Why didn't you select a zero-compression zip?*
This was offered as an initial suggestion - and while it has a lot of similar performance characteristics the need to traverse the entire file means a trade off in initialization speed.
 
 *4. Why didn't you go with SQLLite?*
 This was another strong contender - and supports a _lot_ of the use cases described here (namely complex queries and indexing). Ultimately I didn't want to pull in a larger-ish framework with unknown performance characteristics in. This should be a consideration for anyone looking at this project though :)

*5.  Why aren't subsequent Gets() for the same file cached?*
The expectation is that the Byte[]s returned from Gets() represent data that needs to be parsed and processed further - and that the application developer is responsible for caching this data. Caching the raw byte[] data would introduce complexity and likely additional memory overhead that wouldn't actually be used.