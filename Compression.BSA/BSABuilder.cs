﻿using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Compression.BSA
{
    public class BSABuilder : IDisposable
    {
        internal byte[] _fileId;
        internal uint _version;
        internal uint _offset;
        internal uint _archiveFlags;
        internal uint _folderCount;
        internal uint _fileCount;
        internal uint _totalFolderNameLength;
        internal uint _totalFileNameLength;
        internal uint _fileFlags;

        private List<FileEntry> _files = new List<FileEntry>();
        internal List<FolderRecordBuilder> _folders = new List<FolderRecordBuilder>();

        public BSABuilder()
        {
            _fileId = Encoding.ASCII.GetBytes("BSA\0");
            _offset = 0x24;
        }

        public IEnumerable<FileEntry> Files
        {
            get
            {
                return _files;
            }
        }

        public ArchiveFlags ArchiveFlags
        {
            get
            {
                return (ArchiveFlags)_archiveFlags;
            }
            set
            {
                _archiveFlags = (uint)value;
            }
        }

        public FileFlags FileFlags
        {
            get
            {
                return (FileFlags)_archiveFlags;
            }
            set
            {
                _archiveFlags = (uint)value;
            }
        }

        public VersionType HeaderType
        {
            get
            {
                return (VersionType)_version;
            }
            set
            {
                _version = (uint)value;
            }
        }

        public void AddFile(string path, Stream src, bool flipCompression = false)
        {
            FileEntry r = new FileEntry(this, path, src, flipCompression);

            lock (this)
            {
                _files.Add(r);
            }
        }

        public IEnumerable<string> FolderNames
        {
            get
            {
                return _files.Select(f => Path.GetDirectoryName(f.Path))
                             .ToHashSet();
            }
        }

        public bool HasFolderNames
        {
            get
            {
                return (_archiveFlags & 0x1) > 0;
            }
        }

        public bool HasFileNames
        {
            get
            {
                return (_archiveFlags & 0x2) > 0;
            }
        }

        public bool CompressedByDefault
        {
            get
            {
                return (_archiveFlags & 0x4) > 0;
            }
        }

        public bool HasNameBlobs
        {
            get
            {
                return (_archiveFlags & 0x100) > 0;
            }
        }

        public void Build(string outputName)
        {
            RegenFolderRecords();
            if (File.Exists(outputName)) File.Delete(outputName);

            using (var fs = File.OpenWrite(outputName))
            using (var wtr = new BinaryWriter(fs))
            {
                wtr.Write(_fileId);
                wtr.Write(_version);
                wtr.Write(_offset);
                wtr.Write(_archiveFlags);
                var folders = FolderNames.ToList();
                wtr.Write((uint)folders.Count);
                wtr.Write((uint)_files.Count);
                wtr.Write((uint)_folders.Select(f => f._nameBytes.Count() - 1).Sum()); // totalFolderNameLength
                var s = _files.Select(f => f._pathBytes.Count()).Sum();
                _totalFileNameLength = (uint)_files.Select(f => f._nameBytes.Count()).Sum();
                wtr.Write(_totalFileNameLength); // totalFileNameLength
                wtr.Write(_fileFlags);

                foreach (var folder in _folders)
                {
                    folder.WriteFolderRecord(wtr);
                }

                foreach(var folder in _folders)
                {
                    if (HasFolderNames)
                        wtr.Write(folder._nameBytes);
                    foreach (var file in folder._files)
                    {
                        file.WriteFileRecord(wtr);
                    }
                }

                foreach(var file in _files)
                {
                    wtr.Write(file._nameBytes);
                }

                foreach(var file in _files)
                {
                    file.WriteData(wtr);
                }

     
            }
        }

        public void RegenFolderRecords()
        {
            _folders = _files.GroupBy(f => Path.GetDirectoryName(f.Path).ToLowerInvariant())
                             .Select(f => new FolderRecordBuilder(this, f.Key, f.ToList()))
                             .OrderBy(f => f._hash)
                             .ToList();

            foreach (var folder in _folders)
                foreach (var file in folder._files)
                    file._folder = folder;

            _files = (from folder in _folders
                      from file in folder._files
                      orderby folder._hash, file._hash
                      select file).ToList();
        }

        public void Dispose()
        {
            
        }
    }

    public class FolderRecordBuilder
    {
        internal IEnumerable<FileEntry> _files;
        private string _name;
        internal BSABuilder _bsa;
        internal ulong _hash;
        internal uint _fileCount;
        internal byte[] _nameBytes;
        internal uint _recordSize;
        internal ulong _offset;

        public ulong Hash
        {
            get
            {
                return _hash;
            }
        }

        public string Name
        {
            get
            {
                return _name;
            }
        }

        public ulong SelfSize
        {
            get
            {
                if (_bsa.HeaderType == VersionType.SSE)
                {
                    return sizeof(ulong) + sizeof(uint) + sizeof(uint) + sizeof(ulong);
                }
                else
                {
                    return sizeof(ulong) + sizeof(uint) + sizeof(uint);
                }
            }
        }

        public ulong FileRecordSize
        {
            get
            {
                ulong size = 0;
                if (_bsa.HasFolderNames)
                    size += (ulong)_nameBytes.Length;
                size += (ulong)_files.Select(f => sizeof(ulong) + sizeof(uint) + sizeof(uint)).Sum();
                return size;
            }
        }

        public FolderRecordBuilder(BSABuilder bsa, string folderName, IEnumerable<FileEntry> files)
        {
            _files = files.OrderBy(f => f._hash);
            _name = folderName.ToLowerInvariant();
            _bsa = bsa;
            // Folders don't have extensions, so let's make sure we cut it out
            _hash = _name.GetBSAHash("");
            _fileCount = (uint)files.Count();
            _nameBytes = folderName.ToBZString();
            _recordSize = sizeof(ulong) + sizeof(uint) + sizeof(uint);
        }

        public void WriteFolderRecord(BinaryWriter wtr)
        {
            var idx = _bsa._folders.IndexOf(this);
            _offset = (ulong)wtr.BaseStream.Position;
            _offset += (ulong)_bsa._folders.Skip((int)idx).Select(f => (long)f.SelfSize).Sum();
            _offset += _bsa._totalFileNameLength;
            _offset += (ulong)_bsa._folders.Take((int)idx).Select(f => (long)f.FileRecordSize).Sum();

            var sp =  wtr.BaseStream.Position;
            wtr.Write(_hash);
            wtr.Write(_fileCount);
            if (_bsa.HeaderType == VersionType.SSE)
            {
                wtr.Write((uint)0); // unk
                wtr.Write((ulong)_offset); // offset
            }
        }

    }

    public class FileEntry
    {
        internal FolderRecordBuilder _folder;
        internal BSABuilder _bsa;
        internal string _path;
        internal string _name;
        internal string _filenameSource;
        internal Stream _bytesSource;
        internal bool _flipCompression;

        internal ulong _hash;
        internal byte[] _nameBytes;
        internal byte[] _pathBytes;
        private byte[] _pathBSBytes;
        internal byte[] _rawData;
        internal int _originalSize;
        private long _offsetOffset;

        public FileEntry(BSABuilder bsa, string path, Stream src, bool flipCompression)
        {
            _bsa = bsa;
            _path = path.ToLowerInvariant();
            _name = System.IO.Path.GetFileName(_path);
            _hash = _name.GetBSAHash();
            _nameBytes = _name.ToTermString();
            _pathBytes = _path.ToTermString();
            _pathBSBytes = _path.ToBSString();
            _flipCompression = flipCompression;

            var ms = new MemoryStream();
            src.CopyTo(ms);
            _rawData = ms.ToArray();
            _originalSize = _rawData.Length;

            if (Compressed)
                CompressData();

        }

        private void CompressData()
        {
            if (_bsa.HeaderType == VersionType.SSE)
            {
                var r = new MemoryStream();

                using (var w = LZ4Stream.Encode(r, new LZ4EncoderSettings() { CompressionLevel = LZ4Level.L10_OPT }))
                    (new MemoryStream(_rawData)).CopyTo(w);

                _rawData = r.ToArray();

            }
        }

        public bool Compressed
        {
            get
            {
                if (_flipCompression)
                    return !_bsa.CompressedByDefault;
                else
                    return _bsa.CompressedByDefault;
            }
        }

        public string Path
        {
            get
            {
                return _path;
            }
        }

        public bool FlipCompression
        {
            get
            {
                return _flipCompression;
            }
            set
            {
                _flipCompression = value;
            }
        }

        public ulong Hash { get
            {
                return _hash;
            }
        }

        public FolderRecordBuilder Folder
        {
            get
            {
                return _folder;
            }
        }

        internal void WriteFileRecord(BinaryWriter wtr)

        {
            wtr.Write(_hash);
            var size = _rawData.Length;
            if (_bsa.HasNameBlobs)
            {
                size += _pathBSBytes.Length;
            }
            if (Compressed)
            {
                size += 4;
            }
            if (_flipCompression)
                wtr.Write((uint)size | (0x1 << 30));
            else
                wtr.Write((uint)size);

            _offsetOffset = wtr.BaseStream.Position;
            wtr.Write((uint)0xDEADBEEF);
        }

        internal void WriteData(BinaryWriter wtr)
        {
            uint offset = (uint)wtr.BaseStream.Position;
            wtr.BaseStream.Position = _offsetOffset;
            wtr.Write((uint)offset);
            wtr.BaseStream.Position = offset;

            if (Compressed)
            {
                if (_bsa.HasNameBlobs)
                {
                    wtr.Write(_pathBSBytes);
                }
                wtr.Write((uint)_originalSize);
                wtr.Write(_rawData);
            }
            else
            {
                if (_bsa.HasNameBlobs)
                {
                    wtr.Write(_pathBSBytes);
                }
                wtr.Write(_rawData);
            }
        }
    }
}
