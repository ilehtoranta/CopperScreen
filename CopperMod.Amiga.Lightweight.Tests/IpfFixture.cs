using System.Buffers.Binary;
using System.Text;

namespace CopperMod.Amiga.Lightweight.Tests;

internal static class IpfFixture
{
    internal sealed record Track(int Bits, byte[] Data, int Head = 0, int Cylinder = 0, int Start = 0, bool Weak = false, uint Density = 2);
    internal static byte[] Create(params Track[] tracks)
    {
        using var output = new MemoryStream();
        Chunk(output, "CAPS", []);
        var info = new byte[84];
        Put(info, 0, 1); Put(info, 4, 2); Put(info, 8, 1); Put(info, 28, 83); Put(info, 36, 1);
        Chunk(output, "INFO", info);
        uint id = 0;
        foreach (var track in tracks)
        {
            var imge = new byte[68];
            Put(imge, 0, (uint)track.Cylinder); Put(imge, 4, (uint)track.Head);
            Put(imge, 8, track.Density); Put(imge, 12, 1); Put(imge, 16, (uint)(track.Bits + 7) / 8);
            Put(imge, 24, (uint)track.Start); Put(imge, 28, (uint)track.Bits); Put(imge, 36, (uint)track.Bits);
            Put(imge, 40, track.Density == 1 ? 0u : 1u); Put(imge, 52, ++id);
            Chunk(output, "IMGE", imge);
            if (track.Density == 1) continue;
            var data = new byte[38 + (track.Weak ? 0 : (track.Bits + 7) / 8)];
            Put(data, 0, (uint)track.Bits); Put(data, 16, 2); Put(data, 20, 4); Put(data, 28, 32);
            data[32] = track.Weak ? (byte)0x85 : (byte)0x84;
            Put(data, 33, (uint)track.Bits);
            if (!track.Weak) track.Data.CopyTo(data, 37);
            var header = new byte[16];
            Put(header, 0, (uint)data.Length); Put(header, 4, (uint)data.Length * 8); Put(header, 12, id);
            Chunk(output, "DATA", header);
            output.Write(data);
        }
        return output.ToArray();
    }
    private static void Put(Span<byte> bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes[offset..], value);
    internal static byte[] DensityTrack(uint density, int start = 0)
    {
        const int blocks = 7, dataBits = 10000, gapBits = 2000, bits = blocks * (dataBits + gapBits);
        using var output = new MemoryStream();
        Chunk(output, "CAPS", []);
        var info = new byte[84]; Put(info, 4, 2); Chunk(output, "INFO", info);
        var imge = new byte[68]; Put(imge, 8, density); Put(imge, 12, 1);
        Put(imge, 16, bits / 8); Put(imge, 24, (uint)start); Put(imge, 28, blocks * dataBits);
        Put(imge, 32, blocks * gapBits); Put(imge, 36, bits); Put(imge, 40, blocks); Put(imge, 52, 1);
        Chunk(output, "IMGE", imge);
        const int streamSize = 6 + dataBits / 8;
        var data = new byte[blocks * (32 + streamSize)];
        for (var block = 0; block < blocks; block++)
        {
            var descriptor = block * 32; var stream = blocks * 32 + block * streamSize;
            Put(data, descriptor, dataBits); Put(data, descriptor + 4, gapBits);
            Put(data, descriptor + 16, 2); Put(data, descriptor + 20, 4);
            Put(data, descriptor + 24, 0xAAAAAAAA); Put(data, descriptor + 28, (uint)stream);
            data[stream] = 0x84; Put(data, stream + 1, dataBits);
            data.AsSpan(stream + 5, dataBits / 8).Fill(0xAA);
        }
        var header = new byte[16]; Put(header, 0, (uint)data.Length); Put(header, 4, (uint)data.Length * 8); Put(header, 12, 1);
        Chunk(output, "DATA", header); output.Write(data); return output.ToArray();
    }
    private static void Chunk(Stream output, string name, byte[] payload)
    {
        var header = new byte[12]; Encoding.ASCII.GetBytes(name).CopyTo(header, 0);
        Put(header, 4, (uint)payload.Length + 12); output.Write(header); output.Write(payload);
    }
}
