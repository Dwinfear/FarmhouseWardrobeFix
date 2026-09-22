using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FarmhouseWardrobeFix
{
    internal sealed class NativeBc7Patch
    {
        private static readonly byte[] ExpectedMagic = Encoding.ASCII.GetBytes("FWBC7P01");

        internal List<NativeBc7Region> Regions { get; } = new List<NativeBc7Region>();

        internal static NativeBc7Patch Parse(byte[] bytes)
        {
            var result = new NativeBc7Patch();
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, false);

            byte[] magic = ReadExact(reader, ExpectedMagic.Length);
            if (!EqualBytes(magic, ExpectedMagic))
                throw new InvalidDataException("Patch magic is invalid.");

            uint version = reader.ReadUInt32();
            uint width = reader.ReadUInt32();
            uint height = reader.ReadUInt32();
            uint recordCount = reader.ReadUInt32();
            if (version != 1 || width != 2048 || height != 2048 || recordCount != 8)
                throw new InvalidDataException(
                    $"Patch header is invalid: version={version}, size={width}x{height}, " +
                    $"records={recordCount}.");

            for (uint i = 0; i < recordCount; i++)
            {
                var region = new NativeBc7Region
                {
                    Mip = checked((int)reader.ReadUInt32()),
                    BlockX = checked((int)reader.ReadUInt32()),
                    BlockY = checked((int)reader.ReadUInt32()),
                    BlockWidth = checked((int)reader.ReadUInt32()),
                    BlockHeight = checked((int)reader.ReadUInt32())
                };
                int byteCount = checked((int)reader.ReadUInt32());
                if (region.Mip < 0 || region.Mip >= 12 || region.BlockX < 0 ||
                    region.BlockY < 0 || region.BlockWidth <= 0 || region.BlockHeight <= 0 ||
                    byteCount != checked(region.BlockWidth * region.BlockHeight * 16))
                    throw new InvalidDataException($"Patch region {i} has invalid dimensions.");

                ReadExact(reader, byteCount);
                region.PatchedBytes = ReadExact(reader, byteCount);
                result.Regions.Add(region);
            }

            if (stream.Position != stream.Length)
                throw new InvalidDataException(
                    $"Patch contains {stream.Length - stream.Position} unexpected trailing bytes.");

            return result;
        }

        private static byte[] ReadExact(BinaryReader reader, int count)
        {
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count)
                throw new EndOfStreamException($"Expected {count} bytes, got {bytes.Length}.");
            return bytes;
        }

        private static bool EqualBytes(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }
            return true;
        }
    }

    internal sealed class NativeBc7Region
    {
        internal int Mip { get; set; }
        internal int BlockX { get; set; }
        internal int BlockY { get; set; }
        internal int BlockWidth { get; set; }
        internal int BlockHeight { get; set; }
        internal byte[] PatchedBytes { get; set; }
    }
}
