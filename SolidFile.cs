using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SolidSharp
{
    /// <summary>
    /// Represents a solid compressed file that is either in the process of being authored, or referenced on disk
    /// </summary>
    public class SolidFile : IDisposable
    {
        
        /// <summary>
        /// Whether or not this file is currently flagged as mutable
        /// </summary>
        public bool Mutable { get; }

        /// <summary>
        /// Collection of contents pending write to disk
        /// </summary>
        private readonly Dictionary<string, byte[]> _pendingContents;

        /// <summary>
        /// The global meta blob, if any
        /// </summary>
        public byte[] GlobalMeta { get; private set; }
        
        /// <summary>
        /// Collection of per-entry meta data
        /// </summary>
        private readonly Dictionary<string, SolidEntryMeta> _entryMetaData;

        /// <summary>
        /// Converts tags to integers as they're assigned
        /// </summary>
        private readonly Dictionary<string, int> _tagToTagIndex = new Dictionary<string, int>();

        /// <summary>
        /// Reverse look up of tag to int
        /// </summary>
        private readonly Dictionary<int, string> _tagIndexToTag = new Dictionary<int, string>();
        
        /// <summary>
        /// Lookup of tag id to list of content matching that id
        /// </summary>
        private readonly Dictionary<int, List<SolidEntryMeta>> _tagLookup = new Dictionary<int, List<SolidEntryMeta>>();
        
        /// <summary>
        /// The file version this archive was written with
        /// </summary>
        public int FileVersion { get; private set; }

        /// <summary>
        /// Current version to write into metadata
        /// </summary>
        private const int _solidFileVersion = 1;

        /// <summary>
        /// This is the offset from all the header content. Use when translating per-entry offsets into payload indices
        /// </summary>
        private long _indexOffset;
        
        /// <summary>
        /// The file path, if any, given to us to read from
        /// </summary>
        private string _filePath;

        /// <summary>
        /// The file stream to read from, if any
        /// </summary>
        private FileStream _readingFileStream;
        
        /// <summary>
        /// Create a new solid file in memory
        /// </summary>
        /// <param name="mutable">Whether or not this archive should be initially mutable</param>
        public SolidFile(bool mutable = true)
        {
            Mutable = mutable;
            _pendingContents = new Dictionary<string, byte[]>();
            _entryMetaData = new Dictionary<string, SolidEntryMeta>();
            FileVersion = _solidFileVersion;
        }
        
        /// <summary>
        /// Create a new solid file from a path on disk
        /// </summary>
        /// <param name="filePath">Whether or not this archive should be initially mutable</param>
        public SolidFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new Exception(filePath + "did not exist");
            }

            _filePath = filePath;
            Mutable = false;
            _entryMetaData = new Dictionary<string, SolidEntryMeta>();
            _readingFileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _reader = new BinaryReader(_readingFileStream);
            BeginRead();
        }

        




        /// <summary>
        /// Disposes of this solid file, clearing out any cached state and freeing up any temporary resources
        /// </summary>
        public void Dispose()
        {
            if (_pendingContents != null)
            {
                _pendingContents.Clear();    
            }
            _entryMetaData.Clear();
            if (_readingFileStream != null)
            {
                _readingFileStream.Dispose();
                _readingFileStream = null;
            }
            if (_readingFileStream != null)
            {
                _readingFileStream.Dispose();
                _readingFileStream = null;
            }

            if (_contentMemoryStream != null)
            {
                _contentMemoryStream.Dispose();
                _contentMemoryStream = null;
            }
        }
        


        /// <summary>
        /// Attach an optional global meta data blob to this solid file. This will be read in its entirety on app init.
        /// </summary>
        /// <param name="contents">The raw byte array contents</param>
        public void AttachGlobalMeta(byte[] contents)
        {
            if (!Mutable)
            {
                throw new InvalidOperationException("File was immutable");   
            }

            GlobalMeta = contents;
        }


        /// <summary>
        /// Adds the given file name into this solid file archive. If no ID is provided, the file's absolute file path is used instead
        /// </summary>
        /// <param name="filePath">The absolute file path on disk</param>
        /// <param name="id">The ID to use, if any</param>
        /// <param name="tags">Optional tags to assign to this entry</param>
        /// <param name="metaData">Optional metadata to assign to this entry.</param>
        /// <exception cref="InvalidOperationException">Thrown if the archive is flagged immutable or if this entry already exists, or if the file provided does not exist</exception>
        public void AddFile(string filePath, string id = null, IList<string> tags = null, byte[] metaData = null)
        {
            if (!File.Exists(filePath))
            {
                throw new InvalidOperationException("Provided file " + filePath + " could not be found");
            }

            if (string.IsNullOrEmpty(id))
            {
                id = filePath;
            }

            var rawBytes = File.ReadAllBytes(filePath);
            AddEntry(id, rawBytes, tags, metaData);
        }
        
        /// <summary>
        /// Add a string identified entry into this solid file.
        /// </summary>
        /// <param name="id">The id for this entry</param>
        /// <param name="contents">The raw byte array contents</param>
        /// <param name="tags">Optional tags to assign to this entry.</param>
        /// <param name="metaData">Optional metadata to assign to this entry.</param>
        /// <exception cref="InvalidOperationException">Thrown if the archive is flagged immutable or if this entry already exists</exception>
        public void AddEntry(string id, byte[] contents, IList<string> tags = null, byte[] metaData = null)
        {
            if (!Mutable)
            {
                throw new InvalidOperationException("File was immutable");   
            }

            if (_entryMetaData.ContainsKey(id))
            {
                throw new InvalidOperationException("Entry " + id + " already exists");
            }
            SetEntry(id, contents, tags, metaData);
        }
        
        /// <summary>
        /// Adds the provided solid entry in this archive.
        /// </summary>
        /// <param name="entry">The details associated with this entry</param>
        /// <exception cref="InvalidOperationException">Thrown if the archive is flagged immutable or if this entry already exists</exception>
        public void AddEntry(SolidEntry entry)
        {
            if (!Mutable)
            {
                throw new InvalidOperationException("File was immutable");   
            }

            if (_entryMetaData.ContainsKey(entry.Id))
            {
                throw new InvalidOperationException("Entry " + entry.Id + " already exists");
            }
            SetEntry(entry.Id, entry.Contents, entry.Tags, entry.MetaData);
        }

        
        
        /// <summary>
        /// Sets a string identified entry into this solid file - overriding it if it exists
        /// </summary>
        /// <param name="id">The id for this entry</param>
        /// <param name="contents">The raw byte array contents</param>
        /// <param name="tags">Optional tags to assign to this entry.</param>
        /// <param name="metaData">Optional metadata to assign to this entry.</param>
        /// <exception cref="InvalidOperationException">Thrown if the file is flagged immutable or if this entry already exists</exception>
        public void SetEntry(string id, byte[] contents, IList<string> tags = null, byte[] metaData = null)
        {
            if (!Mutable)
            {
                throw new InvalidOperationException("File was immutable");
            }

            _pendingContents[id] = contents;
            var newMeta = new SolidEntryMeta(id, RemapTags(tags), metaData, contents);
            _entryMetaData[id] = newMeta;
        }

        /// <summary>
        /// Remaps string tags into more compact integer representations
        /// </summary>
        /// <param name="tags">A collection of string tags</param>
        /// <returns>An integer list of tagged values</returns>
        private IList<int> RemapTags(IList<string> tags)
        {
            if (tags == null)
            {
                return null;
            }

            List<int> remapped = new List<int>();
            foreach (var tag in tags)
            {                
                remapped.Add(RemapTag(tag));
            }

            return remapped;
        }

        /// <summary>
        /// Remap a single tag from its string value to its int val
        /// </summary>
        /// <param name="tag">The tag in question</param>
        /// <returns>An integer mapped tag</returns>
        private int RemapTag(string tag)
        {
            int intVal;
            if (!_tagToTagIndex.TryGetValue(tag, out intVal))
            {
                intVal = _tagToTagIndex.Keys.Count + 1;
                _tagToTagIndex[tag] = intVal;
                _tagIndexToTag[intVal] = tag;
            }

            return intVal;
        }

        /// <summary>
        /// Gets a collection of entries that match the provided tag
        /// </summary>
        /// <param name="tag">The tag to query</param>
        /// <returns>A list of deserialized entries that contain the provided tag, if any</returns>
        public List<SolidEntry> GetByTag(string tag)
        {
            
            int tagInt = RemapTag(tag);
            List<SolidEntryMeta> existingMeta;
            if (!_tagLookup.TryGetValue(tagInt, out existingMeta))
            {
                return new List<SolidEntry>();
            }
            List<SolidEntry> entries = new List<SolidEntry>(existingMeta.Count);
            foreach (var meta in existingMeta)
            {
                var entry = GetFromMeta(meta);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
            return entries;
        }
        
        /// <summary>
        /// Gets a collection of entries that match the provided tag
        /// </summary>
        /// <param name="tag">The tag to query</param>
        /// <returns>A list of deserialized entries that contain the provided tag, if any</returns>
        public List<SolidEntryMeta> GetMetaByTag(string tag)
        {
            
            int tagInt = RemapTag(tag);
            List<SolidEntryMeta> existingMeta;
            if (!_tagLookup.TryGetValue(tagInt, out existingMeta))
            {
                return new List<SolidEntryMeta>();
            }

            return existingMeta;
        }

        /// <summary>
        /// Gets an entry from this archive if it exists
        /// </summary>
        /// <param name="entryName">The string ID to identify the entry with</param>
        /// <returns>The entry for the given file, if it exists, null otherwise</returns>
        public SolidEntry Get(string entryName)
        {
            SolidEntryMeta meta;
            if (!_entryMetaData.TryGetValue(entryName, out meta))
            {
                return null;
            }
            return GetFromMeta(meta);
        }

        public SolidEntry GetFromMeta(SolidEntryMeta meta)
        {
            SolidEntry entry = new SolidEntry();
            entry.Id = meta.Id;

            if (Mutable) // Support the use case when we're requesting a read on an archive thats being build
            {
                entry.Contents = _pendingContents[entry.Id];
            }
            else
            {
                _reader.BaseStream.Position = meta.ContentOffset + _indexOffset; // Scrub the reader to the position
                entry.Contents = _reader.ReadBytes(meta.ContentLength);    
            }
            

            if (meta.Tags != null)
            {
                int tagCnt = meta.Tags.Count;
                string[] tags = new string[tagCnt];
                for (int i = 0; i < tagCnt; i++)
                {
                    tags[i] = _tagIndexToTag[meta.Tags[i]];
                }
                entry.Tags = new List<string>(tags);
            }

            entry.ContentOffset = meta.ContentOffset;
            entry.ContentLength = meta.ContentLength;
            entry.MetaData = meta.Meta;
            
            return entry;
        }
        
        /// <summary>
        /// Write this solid file to the given file path
        /// </summary>
        /// <param name="filePath">The absolute path to write this solid file to</param>
        /// <returns>A build report with details on the file that was generated</returns>
        public SolidBuildReport Write(string filePath)
        {
            SolidBuildReport report;
            using (var ms = new MemoryStream())
            {
                report = Build(ms);
                if (report.Successful)
                {
                    var bytes = ms.ToArray();
                    File.WriteAllBytes(filePath, bytes);
                }
            }

            return report;

        }
        
        /// <summary>
        /// Writes this solid file into the provided stream
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        /// <returns>A build report with details on the file that was generated</returns>
        /// <exception cref="Exception">Bubbles up any underlying exceptions</exception>
        public SolidBuildReport Build(Stream stream)
        {
            
            _writer = stream;
            _report = new SolidBuildReport {Successful = true};
            try
            {
                BeginWrite();
            }
            catch (Exception)
            {
                _report.Successful = false;
                throw;
            }
            finally
            {
                _writer = null; // Stop referencing the writer we were passed to it can be freed if necessary
            }

            return _report;
        }

#region Internal Read/Write methods
        
        /// <summary>
        /// The reader in flight, if any
        /// </summary>
        private BinaryReader _reader;
        /// <summary>
        /// The writer in flight, if any
        /// </summary>
        private Stream _writer;
        
        /// <summary>
        /// The content writer in flight, if any
        /// </summary>
        private MemoryStream _contentMemoryStream;
        
        /// <summary>
        /// The build report being generated, if any
        /// </summary>
        private SolidBuildReport _report;
        

        
        private int Write(int val)
        {
            var bytes = BitConverter.GetBytes(val);
            _writer.Write(bytes, 0, bytes.Length);
            return bytes.Length;
        }
        
        private int Write(long val)
        {
            var bytes = BitConverter.GetBytes(val);
            _writer.Write(bytes, 0, bytes.Length);
            return bytes.Length;
        }

        private void BeginWrite()
        {
            WriteVersionNumber();
            WriteGlobalMetaData();
            WriteTagIndex();
            WriteTempContentMemoryStream();
            WriteEntryMetaData();
            WriteEntryContents();
            
        }

        private void WriteTagIndex()
        {
            Write(_tagToTagIndex.Keys.Count); // Write the count of tags present
            
            foreach (var kvp in _tagToTagIndex)
            {
                _report.UniqueTags++;
                
                var bytes = Encoding.UTF8.GetBytes(kvp.Key);
                _report.MetaLength += Write(bytes.Length); // Write the length of the tag
                _writer.Write(bytes, 0, bytes.Length); // Write the tag
                _report.MetaLength += bytes.Length;
                _report.MetaLength += Write(kvp.Value); // Write the tag ID
            }
        }

        private void WriteEntryContents()
        {
            _contentMemoryStream.Position = 0; // Rewind the holding buffer for our content
            _contentMemoryStream.CopyTo(_writer); // Flush the entire contents of the payload bytes into the writer
        }

        private void WriteEntryMetaData()
        {
            _report.MetaLength += Write(_entryMetaData.Count); // Write the number of entries first
            foreach (var kvp in _entryMetaData)
            {
                WriteMetaEntry(kvp.Key, kvp.Value);
            }
        }

        private void WriteMetaEntry(string contentId, SolidEntryMeta meta)
        {
            var bytes = Encoding.UTF8.GetBytes(contentId);
            _report.MetaLength += Write(bytes.Length); // Write the length of the content id
            _writer.Write(bytes, 0, bytes.Length); // Write the content ID
            _report.MetaLength += bytes.Length;

            _report.MetaLength += Write(meta.ContentOffset);
            _report.MetaLength += Write(meta.ContentLength);
            
            if (meta.Tags != null)
            {
                _report.MetaLength += Write(meta.Tags.Count); // Write the length of the tags
                foreach (var tag in meta.Tags)
                {
                    _report.MetaLength += Write(tag); // Write the integer value of the tag
                }
            }
            else
            {
                _report.MetaLength += Write(0); // No tags provided, record zero len
            }

            if (meta.Meta != null)
            {
                _report.MetaLength += Write(meta.Meta.Length); // Write the length of the meta file
                _writer.Write(meta.Meta, 0, meta.Meta.Length);
                _report.MetaLength += meta.Meta.Length;
            }
            else
            {
                _report.MetaLength += Write(0); // No per-entry meta provided. Record zero len
            }
        }

        private void WriteTempContentMemoryStream()
        {
            _contentMemoryStream = new MemoryStream();
            long offset = 0;
            foreach (var kvp in _pendingContents)
            {
                var meta = _entryMetaData[kvp.Key];
                if (meta == null)
                {
                    throw new InvalidOperationException(kvp.Key + " had no metadata entry");
                }

                var len = kvp.Value.Length;
                meta.ContentOffset = offset;
                _contentMemoryStream.Write(kvp.Value, 0, len);
                offset += len;
                _report.NumEntries++;
                _report.PayloadLength += offset;
            }
        }

        private void WriteVersionNumber()
        {
            _report.MetaLength += Write(FileVersion);
        }
        
        private void WriteGlobalMetaData()
        {
            if (GlobalMeta == null)
            {
                Write(0);// Zero length
                return;
            }
            _report.MetaLength += Write(GlobalMeta.Length); // Write the length of the meta we were provided
            _writer.Write(GlobalMeta, 0, GlobalMeta.Length); // Write the meta itself
            _report.MetaLength += GlobalMeta.Length;
        }

        
        
        private void BeginRead()
        {
            ReadVersionNumber();
            ReadGlobalMetaData();
            ReadTagIndex();
            ReadEntryMetaData();
            _indexOffset = _reader.BaseStream.Position; // Record the position of the reader once we've parsed everything. This informs our jump to read content
        }

        private void ReadVersionNumber()
        {
            FileVersion = _reader.ReadInt32();
            
        } 
        
        private void ReadGlobalMetaData()
        {
            int metaLen = _reader.ReadInt32();
            if (metaLen > 0)
            {
                GlobalMeta = _reader.ReadBytes(metaLen);
            }
        } 
        
        private void ReadEntryMetaData()
        {
            int metaCount = _reader.ReadInt32();
            for(int i = 0; i < metaCount; i++)
            {
                ReadNextEntryMetaData();
            }
        }
        
        private void ReadTagIndex()
        {
            int tagCount = _reader.ReadInt32();
            for(int i = 0; i < tagCount; i++)
            {
                int tagStrLen = _reader.ReadInt32();
                var tagStrBytes = _reader.ReadBytes(tagStrLen);
                string tag = Encoding.UTF8.GetString(tagStrBytes);
                int tagId = _reader.ReadInt32();
                _tagToTagIndex[tag] = tagId;
                _tagIndexToTag[tagId] = tag;
            }
        }
        

        private void ReadNextEntryMetaData()
        {
            
            int contentIdLen = _reader.ReadInt32();
            var contentIdBytes = _reader.ReadBytes(contentIdLen);
            string contentId = Encoding.UTF8.GetString(contentIdBytes);
            long contentOffset = _reader.ReadInt64();
            int contentLen = _reader.ReadInt32();
                
            int tagCount = _reader.ReadInt32();
            int[] tagArr = new int[tagCount];
            IList<int> tags = null;
            if (tagCount > 0)
            {
                
                for (int i = 0; i < tagCount; i++)
                {
                    int tagInt = _reader.ReadInt32();
                    tagArr[i] = tagInt;
                }

                tags = new List<int>(tagArr);
            }
            
            int metaLen = _reader.ReadInt32();
            byte[] meta = null;
            if (metaLen > 0)
            {
                meta = _reader.ReadBytes(metaLen);
            }
            var metaEntry = new SolidEntryMeta(contentId, tags, meta, contentLen, contentOffset);
            
            _entryMetaData[contentId] = metaEntry;
            for (int i = 0; i < tagCount; i++)
            {
                int tagId = tagArr[i];
                List<SolidEntryMeta> existing;
                if (!_tagLookup.TryGetValue(tagId, out existing))
                {
                    existing = new List<SolidEntryMeta>();
                    _tagLookup[tagId] = existing;
                }
                existing.Add(metaEntry);
            }
        }
        
#endregion        
        


        /// <summary>
        /// Returns a diagnostic stream detailing this archive after it's been parsed 
        /// </summary>
        /// <returns>A diagnostic string with various details about this archive</returns>
        public string Describe()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Solid File - Version " + FileVersion);
            if (!string.IsNullOrEmpty(_filePath))
            {
                sb.AppendLine("Loaded from file " + _filePath);
            }

            if (GlobalMeta != null)
            {
                sb.AppendLine("Global Meta Data Length " + GlobalMeta.Length);    
            }
            else
            {
                sb.AppendLine("No global metadata");
            }

            sb.AppendLine("Total Entries " + _entryMetaData.Keys.Count);
            sb.AppendLine("Unique Tags " + _tagToTagIndex.Keys.Count);
            sb.AppendLine("Index offset " + _indexOffset);
            return sb.ToString();
        }
    }
       


}
