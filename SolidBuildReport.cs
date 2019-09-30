namespace SolidSharp
{
    /// <summary>
    /// Represents a build report
    /// </summary>
    public class SolidBuildReport
    {
        /// <summary>
        /// Whether or not the solid file was built succesfully
        /// </summary>
        public bool Successful;
        
        /// <summary>
        /// The total number of entries that were written 
        /// </summary>
        public int NumEntries;

        /// <summary>
        /// The total byte length of the produced solid file
        /// </summary>
        public long TotalLength;
        
        /// <summary>
        /// The length of the included meta data
        /// </summary>
        public long MetaLength;

        /// <summary>
        /// The combined length of all entries that were written
        /// </summary>
        public long PayloadLength;

        /// <summary>
        /// The total length of per-entry-metadata that was written
        /// </summary>
        public long PerEntryMetaLength;

        /// <summary>
        /// The number of unique tags that were used when adding entries
        /// </summary>
        public long UniqueTags;
    }

}