using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    public class Object
    {
        public SerializedFile assetsFile;
        public ObjectReader reader;
        public long m_PathID;
        public int[] version;
        protected BuildType buildType;
        public BuildTarget platform;
        public ClassIDType type;
        public SerializedType serializedType;
        public uint byteSize;

        public Object(ObjectReader reader)
        {
            this.reader = reader;
            reader.Reset();
            assetsFile = reader.assetsFile;
            type = reader.type;
            m_PathID = reader.m_PathID;
            version = reader.version;
            buildType = reader.buildType;
            platform = reader.platform;
            serializedType = reader.serializedType;
            byteSize = reader.byteSize;

            if (platform == BuildTarget.NoTarget)
            {
                reader.ReadUInt32();  // m_ObjectHideFlags
            }
        }

        protected bool HasStructMember(string name)
        {
            return serializedType?.m_Nodes != null && serializedType.m_Nodes.Any(x => x.m_Name == name);
        }

        public string Dump()
        {
            if (serializedType?.m_Nodes == null) return null;
            
            var sb = new StringBuilder();
            TypeTreeHelper.ReadTypeString(sb, serializedType.m_Nodes, reader);
            return sb.ToString();
        }

        public string Dump(List<TypeTreeNode> m_Nodes)
        {
            if (m_Nodes == null) return null;
            
            var sb = new StringBuilder();
            TypeTreeHelper.ReadTypeString(sb, m_Nodes, reader);
            return sb.ToString();
        }

        public OrderedDictionary ToType()
        {
            return serializedType?.m_Nodes != null ? TypeTreeHelper.ReadType(serializedType.m_Nodes, reader) : null;
        }

        public OrderedDictionary ToType(List<TypeTreeNode> m_Nodes)
        {
            return m_Nodes != null ? TypeTreeHelper.ReadType(m_Nodes, reader) : null;
        }

        public byte[] GetRawData()
        {
            reader.Reset();
            return reader.ReadBytes((int)byteSize);
        }
    }
}
