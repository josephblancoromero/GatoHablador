using System.Text;

namespace GatoHablador.Core;

public static class BridgeProtocol
{
    public const byte Magic = 0xA7;
    public const byte Version = 1;
    public const int FrameSize = 64;
    public const int HeaderSize = 6;
    public const int PayloadCapacity = 56;
    public const int ProtectedSize = HeaderSize + PayloadCapacity;
    public const int Columns = 64;
    public const int Rows = 8;

    public static IReadOnlyList<byte[]> Packetize(byte sequence, ReadOnlySpan<byte> payload)
    {
        var chunkCount = Math.Max(1, (payload.Length + PayloadCapacity - 1) / PayloadCapacity);
        if (chunkCount > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), "The encoded message is too large.");
        }

        var frames = new List<byte[]>(chunkCount);
        for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            var frame = new byte[FrameSize];
            frame[0] = Magic;
            frame[1] = Version;
            frame[2] = sequence;
            frame[3] = checked((byte)chunkIndex);
            frame[4] = checked((byte)chunkCount);

            var sourceOffset = chunkIndex * PayloadCapacity;
            var length = Math.Min(PayloadCapacity, payload.Length - sourceOffset);
            frame[5] = checked((byte)Math.Max(0, length));
            if (length > 0)
            {
                payload.Slice(sourceOffset, length).CopyTo(frame.AsSpan(HeaderSize));
            }

            var crc = Crc16Ccitt.Compute(frame.AsSpan(0, ProtectedSize));
            frame[62] = (byte)(crc >> 8);
            frame[63] = (byte)(crc & 0xFF);
            frames.Add(frame);
        }

        return frames;
    }

    public static bool TryDecode(ReadOnlySpan<byte> frame, out BridgeChunk chunk)
    {
        chunk = default;
        if (frame.Length != FrameSize || frame[0] != Magic || frame[1] != Version)
        {
            return false;
        }

        var chunkIndex = frame[3];
        var chunkCount = frame[4];
        var payloadLength = frame[5];
        if (chunkCount == 0 || chunkIndex >= chunkCount || payloadLength > PayloadCapacity)
        {
            return false;
        }

        var expected = (ushort)((frame[62] << 8) | frame[63]);
        if (Crc16Ccitt.Compute(frame[..ProtectedSize]) != expected)
        {
            return false;
        }

        chunk = new BridgeChunk(
            frame[2],
            chunkIndex,
            chunkCount,
            frame.Slice(HeaderSize, payloadLength).ToArray());
        return true;
    }

    public static byte[] BitsToFrame(ReadOnlySpan<bool> bits)
    {
        if (bits.Length != Columns * Rows)
        {
            throw new ArgumentException("A frame requires exactly 512 bits.", nameof(bits));
        }

        var frame = new byte[FrameSize];
        for (var byteIndex = 0; byteIndex < frame.Length; byteIndex++)
        {
            byte value = 0;
            for (var bit = 0; bit < 8; bit++)
            {
                if (bits[(byteIndex * 8) + bit])
                {
                    value |= checked((byte)(1 << (7 - bit)));
                }
            }

            frame[byteIndex] = value;
        }

        return frame;
    }

    public static bool[] FrameToBits(ReadOnlySpan<byte> frame)
    {
        if (frame.Length != FrameSize)
        {
            throw new ArgumentException("A bridge frame requires exactly 64 bytes.", nameof(frame));
        }

        var bits = new bool[Columns * Rows];
        for (var byteIndex = 0; byteIndex < frame.Length; byteIndex++)
        {
            for (var bit = 0; bit < 8; bit++)
            {
                bits[(byteIndex * 8) + bit] = (frame[byteIndex] & (1 << (7 - bit))) != 0;
            }
        }

        return bits;
    }

    public static byte[] EncodeMessage(ChatMessage message) =>
        Encoding.UTF8.GetBytes(string.Join('\t', Escape(message.Event), Escape(message.Author), Escape(message.Text)));

    public static bool TryParseMessage(ReadOnlySpan<byte> payload, out ChatMessage message)
    {
        message = default!;
        string wire;
        try
        {
            wire = new UTF8Encoding(false, true).GetString(payload);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        var fields = wire.Split('\t');
        if (fields.Length != 3)
        {
            return false;
        }

        message = new ChatMessage(Unescape(fields[0]), Unescape(fields[1]), Unescape(fields[2]), DateTimeOffset.Now);
        return message.Event.Length is > 0 and <= 64 && message.Author.Length <= 256 && message.Text.Length <= 4096;
    }

    private static string Escape(string value) => value
        .Replace("%", "%25", StringComparison.Ordinal)
        .Replace("\t", "%09", StringComparison.Ordinal)
        .Replace("\r", "%0D", StringComparison.Ordinal)
        .Replace("\n", "%0A", StringComparison.Ordinal);

    private static string Unescape(string value) => value
        .Replace("%0A", "\n", StringComparison.OrdinalIgnoreCase)
        .Replace("%0D", "\r", StringComparison.OrdinalIgnoreCase)
        .Replace("%09", "\t", StringComparison.OrdinalIgnoreCase)
        .Replace("%25", "%", StringComparison.OrdinalIgnoreCase);
}

public readonly record struct BridgeChunk(byte Sequence, byte Index, byte Count, byte[] Payload);

public sealed record ChatMessage(string Event, string Author, string Text, DateTimeOffset ReceivedAt);

