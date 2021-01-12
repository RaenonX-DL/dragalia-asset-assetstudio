using System;
using System.IO;

namespace AssetStudio
{
    public class ObjectReader : EndianBinaryReader
    {
        public SerializedFile assetsFile;
        public long m_PathID;
        public long byteStart;
        public uint byteSize;
        public ClassIDType type;
        public SerializedType serializedType;
        public BuildTarget platform;
        public uint m_Version;

        public ObjectInfo objectInfo;

        public int[] version => assetsFile.version;
        public BuildType buildType => assetsFile.buildType;

        public ObjectReader(EndianBinaryReader reader, SerializedFile assetsFile, ObjectInfo objectInfo) : base(reader.BaseStream, reader.endian)
        {
            this.assetsFile = assetsFile;
            m_PathID = objectInfo.m_PathID;
            byteStart = objectInfo.byteStart;
            byteSize = objectInfo.byteSize;
            if (Enum.IsDefined(typeof(ClassIDType), objectInfo.classID))
            {
                type = (ClassIDType)objectInfo.classID;
            }
            else
            {
                type = ClassIDType.UnknownType;
            }
            serializedType = objectInfo.serializedType;
            platform = assetsFile.m_TargetPlatform;
            m_Version = assetsFile.header.m_Version;

            this.objectInfo = objectInfo;
        }

        public void Reset()
        {
            Position = byteStart;
        }
        
        public ObjectReader Duplicate()
        {
            var newStream = new MemoryStream();
            var position = Position;
                        
            // Copy stream
            Position = 0;
            BaseStream.CopyTo(newStream);
            // Reset position
            Position = position;
            newStream.Position = position;

            var newEndianReader = new EndianBinaryReader(newStream, endian);

            return new ObjectReader(newEndianReader, assetsFile, objectInfo);
        }
    }
}
