using System.Collections.Generic;

namespace SolidSharp
{
    /// <summary>
    /// Represents a single parsed file entry in a solid archive
    /// </summary>
    public class SolidEntry
    {   
        /// <summary>
        /// The id of this entry
        /// </summary>
        public string Id;
        
        /// <summary>
        /// The contents of this entry
        /// </summary>
        public byte[] Contents;

        /// <summary>
        /// The parsed tags for this entry
        /// </summary>
        public IList<string> Tags;

        /// <summary>
        /// Any entry metadata attached to this entry
        /// </summary>
        public byte[] MetaData;

        /// <summary>
        /// The offset into the content payload body - Generally only for diagnostic purposes
        /// </summary>
        public long ContentOffset { get; internal set; }
        
        /// <summary>
        /// The length of the underlying content (should match Contents.length)
        /// </summary>
        public int ContentLength { get; internal set; }
    }
}