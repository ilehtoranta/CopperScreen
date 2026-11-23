/*
 * Copyright (C) 2026 Ilkka Lehtoranta
 * SPDX-License-Identifier: MIT
 */

namespace CopperDisk;

/// <summary>Opens or inspects the logical sectors of a hardfile and creates dynamic VHDs.</summary>
public static class AmigaHardfileImage
{
    /// <summary>Create a blank dynamic VHD in an empty file and return its writable logical sectors.
    /// The caller owns the backing file and must keep it exclusively open for the lifetime of the returned stream.</summary>
    public static Stream CreateDynamicVhd(FileStream destination, long capacity)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return new VhdStream(destination, VhdImage.CreateDynamic(destination, capacity), leaveOpen: true);
    }

    /// <summary>Open an image as a seekable, read-only stream and exclude concurrent writers.</summary>
    public static Stream OpenRead(string path)
    {
        var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            var vhd = VhdImage.Open(source);
            if (vhd != null) return new VhdStream(source, vhd);
            source.Position = 0;
            return source;
        }
        catch { source.Dispose(); throw; }
    }

    /// <summary>Get the guest capacity and container type from a caller-owned file without changing its position.</summary>
    public static (long Capacity, bool IsVhd) Inspect(FileStream source)
    {
        var position = source.Position;
        try
        {
            var vhd = VhdImage.Open(source);
            return (vhd?.Length ?? source.Length, vhd != null);
        }
        finally { source.Position = position; }
    }

    internal sealed class VhdStream(FileStream source, VhdImage vhd, bool leaveOpen = false) : Stream
    {
        private long _position;
        private bool _disposed;
        public override bool CanRead => !_disposed && source.CanRead;
        public override bool CanSeek => !_disposed && source.CanSeek;
        public override bool CanWrite => !_disposed && source.CanWrite;
        public override long Length
        {
            get { ObjectDisposedException.ThrowIf(_disposed, this); return vhd.Length; }
        }
        public override long Position
        {
            get { ObjectDisposedException.ThrowIf(_disposed, this); return _position; }
            set
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                _position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(!CanRead, this);
            var count = (int)Math.Min(buffer.Length, Math.Max(0, Length - _position));
            if (count == 0) return 0;
            vhd.Read(source, _position, buffer[..count]);
            _position += count;
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var start = origin switch
            {
                SeekOrigin.Begin => 0,
                SeekOrigin.Current => _position,
                SeekOrigin.End => Length,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            return Position = checked(start + offset);
        }

        public override void Flush()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (CanWrite) source.Flush(flushToDisk: true);
        }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            Write(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!CanWrite) throw new NotSupportedException("The VHD stream is read-only.");
            if (_position > Length || buffer.Length > Length - _position)
                throw new IOException("Cannot write beyond the VHD's virtual capacity.");
            vhd.Write(source, _position, buffer, hook: null);
            _position += buffer.Length;
        }

        protected override void Dispose(bool disposing)
        {
            _disposed = true;
            if (disposing && !leaveOpen) source.Dispose();
            base.Dispose(disposing);
        }
    }
}
