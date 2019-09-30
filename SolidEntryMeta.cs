using System.Collections.Generic;

namespace SolidSharp
{
    /// <summary>
    /// Represents the metadata associated with a solid entry
    /// </summary>
    public class SolidEntryMeta
    {
        /// <summary>
        /// The ID associated with this entry meta
        /// </summary>
        public string Id;
        
        /// <summary>
        /// The tags associated with this entry, if any
        /// </summary>
        public IList<int> Tags;

        /// <summary>
        /// User defined metadata, if any
        /// </summary>
        public byte[] Meta;
        
        /// <summary>
        /// The length of the underlying content, in bytes
        /// </summary>
        public int ContentLength { get; }
        
        /// <summary>
        /// The offset of the content, relative to the beginning of content
        /// </summary>
        public long ContentOffset { get; internal set; }
        
        /// <summary>
        /// Create a new meta data entry with the given tags
        /// </summary>
        /// <param name="tags"></param>
        /// <param name="meta"></param>
        /// <param name="contents"></param>
        internal SolidEntryMeta(string id, IList<int> tags, byte[] meta, byte[] contents)
        {
            Id = id;
            Tags = tags;
            Meta = meta;
            ContentLength = contents.Length;
        }
        
        /// <summary>
        /// Create a new meta data entry with the given tags
        /// </summary>
        /// <param name="tags"></param>
        /// <param name="meta"></param>
        /// <param name="contents"></param>
        internal SolidEntryMeta(string id, IList<int> tags, byte[] meta, int contentLength, long contentOffset)
        {
            Id = id;
            Tags = tags;
            Meta = meta;
            ContentLength = contentLength;
            ContentOffset = contentOffset;
        }
    }

}