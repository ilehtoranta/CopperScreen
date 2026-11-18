using System.Buffers.Binary;
using System.Text;
using CopperDisk;

namespace CopperDisk.Tests;

// Test-only preservation container producer. No third-party media.
internal static class FileSystemIpfFixture
{
    internal static byte[] Create(byte[] adf, int missingTrack = -1, int conflictingTrack = -1, int weakTrack = -1)
    {
        using var output = new MemoryStream();
        void Chunk(string tag, byte[] data, byte[]? trailing = null)
        {
            output.Write(Encoding.ASCII.GetBytes(tag)); output.Write(Words((uint)(12 + data.Length), 0)); output.Write(data);
            if (trailing != null) output.Write(trailing);
        }
        Chunk("CAPS", []);
        Chunk("INFO", Words(1,1,1,0,0,0,0,79,0,1,0,0,1,0,0,0,1,0,0,0,0));
        var disk = AmigaDiskLoader.FromAdfBytes(adf);
        for (var track = 0; track < 160; track++)
        {
            if (track == missingTrack) continue;
            var bytes = AmigaDosTrackEncoder.EncodeTrack(disk, track / 2, track % 2);
            if (track == conflictingTrack)
            {
                var different = (byte[])adf.Clone(); different[(track * 11 + 1) * 512] ^= 0x7f;
                bytes = bytes.Concat(AmigaDosTrackEncoder.EncodeTrack(AmigaDiskLoader.FromAdfBytes(different), track / 2, track % 2)).ToArray();
            }
            var bits = checked((uint)bytes.Length * 8); var id = (uint)track + 1;
            Chunk("IMGE", Words((uint)(track/2),(uint)(track%2),2,1,(uint)bytes.Length,0,0,bits,0,bits,1,0,0,id,0,0,0));
            using var payload = new MemoryStream();
            payload.Write(Words(bits,0,(uint)bytes.Length,0,2,0,0,32));
            if (track == weakTrack) { payload.WriteByte(0x45); payload.WriteByte((byte)(bytes.Length >> 8)); payload.WriteByte((byte)bytes.Length); }
            else { payload.WriteByte(0x44); payload.WriteByte((byte)(bytes.Length >> 8)); payload.WriteByte((byte)bytes.Length); payload.Write(bytes); }
            payload.WriteByte(0);
            Chunk("DATA", Words((uint)payload.Length,(uint)payload.Length*8,0,id), payload.ToArray());
        }
        return output.ToArray();
    }

    private static byte[] Words(params uint[] values)
    {
        var bytes = new byte[values.Length * 4];
        for (var i=0; i<values.Length; i++) BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(i*4), values[i]);
        return bytes;
    }
}
