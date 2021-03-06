﻿using System;
using System.IO;

namespace AssetStudio
{
    public enum EndianType
    {
        LittleEndian,
        BigEndian
    }

    public class EndianBinaryStream : IDisposable
    {
        private Stream stream;
        private EndianType endian;

        private long initPosition;

        public EndianBinaryStream(Stream stream, EndianType endian = EndianType.BigEndian)
        {
            this.stream = stream;
            this.endian = endian;
            initPosition = stream.Position;
        }

        public EndianBinaryReader InitReader()
        {
            var newStream = new MemoryStream();
            stream.Position = 0;  // Make sure the base stream is at position 0
            stream.CopyTo(newStream);
            newStream.Position = initPosition;
            return new EndianBinaryReader(newStream, endian);
        }

        public EndianBinaryReader InitReader(EndianType endianType)
        {
            var newStream = new MemoryStream();
            stream.Position = 0;  // Make sure the base stream is at position 0
            stream.CopyTo(newStream);
            newStream.Position = initPosition;
            return new EndianBinaryReader(newStream, endianType);
        }

        public long Length => stream.Length;

        public void Dispose()
        {
            stream?.Dispose();
        }
    }

    public class EndianBinaryReader : BinaryReader
    {
        public EndianType endian;

        public EndianBinaryReader(Stream stream, EndianType endian = EndianType.BigEndian) : base(stream)
        {
            this.endian = endian;
        }

        public long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        public override short ReadInt16()
        {
            if (endian == EndianType.BigEndian)
            {
                var buff = ReadBytes(2);
                Array.Reverse(buff);
                return BitConverter.ToInt16(buff, 0);
            }
            return base.ReadInt16();
        }

        public override int ReadInt32()
        {
            if (endian == EndianType.BigEndian)
            {
                var buff = ReadBytes(4);
                Array.Reverse(buff);
                return BitConverter.ToInt32(buff, 0);
            }
            return base.ReadInt32();
        }

        public override long ReadInt64()
        {
            if (endian == EndianType.BigEndian)
            {
                var buff = ReadBytes(8);
                Array.Reverse(buff);
                return BitConverter.ToInt64(buff, 0);
            }
            return base.ReadInt64();
        }

        public override ushort ReadUInt16()
        {
            if (endian == EndianType.BigEndian)
            {
                var buff = ReadBytes(2);
                Array.Reverse(buff);
                return BitConverter.ToUInt16(buff, 0);
            }
            return base.ReadUInt16();
        }

        public override uint ReadUInt32()
        {
            if (endian == EndianType.BigEndian)
            {
                var buff = ReadBytes(4);
                Array.Reverse(buff);
                return BitConverter.ToUInt32(buff, 0);
            }
            return base.ReadUInt32();
        }

        public override ulong ReadUInt64()
        {
            if (endian == EndianType.BigEndian)
            {
                var buff = ReadBytes(8);
                Array.Reverse(buff);
                return BitConverter.ToUInt64(buff, 0);
            }
            return base.ReadUInt64();
        }

        public override float ReadSingle()
        {
            if (endian == EndianType.BigEndian)
            {
                var buff = ReadBytes(4);
                Array.Reverse(buff);
                return BitConverter.ToSingle(buff, 0);
            }
            return base.ReadSingle();
        }

        public override double ReadDouble()
        {
            if (endian == EndianType.BigEndian)
            {
                var buff = ReadBytes(8);
                Array.Reverse(buff);
                return BitConverter.ToUInt64(buff, 0);
            }
            return base.ReadDouble();
        }
    }
}
