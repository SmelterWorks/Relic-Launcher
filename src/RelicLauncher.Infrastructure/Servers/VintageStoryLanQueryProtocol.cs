using System.Text;

namespace RelicLauncher.Infrastructure.Servers;

internal static class VintageStoryLanQueryProtocol
{
    // Length-prefixed Client packet: ServerQuery (id=15) with empty query field.
    internal static ReadOnlyMemory<byte> QueryPacket { get; } = new byte[] { 0x04, 0x08, 0x0F, 0x52, 0x00 };

    internal static bool TryParseQueryAnswer(ReadOnlySpan<byte> payload, out QueryAnswer answer)
    {
        answer = new QueryAnswer();
        if (payload.Length < 2)
        {
            return false;
        }

        var offset = 0;
        if (!TryReadLengthPrefix(payload, ref offset, out var messageLength))
        {
            return false;
        }

        var end = offset + messageLength;
        if (end > payload.Length)
        {
            return false;
        }

        var found = false;
        while (offset < end)
        {
            if (!TryReadTag(payload, ref offset, out var fieldNumber, out var wireType))
            {
                return found;
            }

            if (fieldNumber == 28 && wireType == 2)
            {
                if (!TryReadLengthDelimited(payload, ref offset, out var nestedSpan))
                {
                    return found;
                }

                ParseQueryAnswerFields(nestedSpan, ref answer);
                found = true;
                continue;
            }

            if (!SkipField(payload, ref offset, wireType))
            {
                return found;
            }
        }

        return found;
    }

    private static void ParseQueryAnswerFields(ReadOnlySpan<byte> span, ref QueryAnswer answer)
    {
        var offset = 0;
        while (offset < span.Length)
        {
            if (!TryReadTag(span, ref offset, out var fieldNumber, out var wireType))
            {
                break;
            }

            switch (fieldNumber)
            {
                case 1 when wireType == 2:
                    answer.Name = ReadString(span, ref offset);
                    break;
                case 2 when wireType == 2:
                    answer.Motd = ReadString(span, ref offset);
                    break;
                case 3 when wireType == 0:
                    answer.PlayerCount = ReadVarint(span, ref offset);
                    break;
                case 4 when wireType == 0:
                    answer.MaxPlayers = ReadVarint(span, ref offset);
                    break;
                case 6 when wireType == 0:
                    answer.HasPassword = ReadVarint(span, ref offset) != 0;
                    break;
                case 7 when wireType == 2:
                    answer.ServerVersion = ReadString(span, ref offset);
                    break;
                default:
                    SkipField(span, ref offset, wireType);
                    break;
            }
        }
    }

    private static bool TryReadLengthPrefix(ReadOnlySpan<byte> data, ref int offset, out int length)
    {
        length = 0;
        var shift = 0;
        while (offset < data.Length)
        {
            var b = data[offset++];
            length |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return true;
            }

            shift += 7;
            if (shift > 35)
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryReadTag(ReadOnlySpan<byte> data, ref int offset, out int fieldNumber, out int wireType)
    {
        fieldNumber = 0;
        wireType = 0;
        if (!TryReadVarint(data, ref offset, out var tag))
        {
            return false;
        }

        wireType = tag & 0x07;
        fieldNumber = tag >> 3;
        return true;
    }

    private static bool TryReadLengthDelimited(ReadOnlySpan<byte> data, ref int offset, out ReadOnlySpan<byte> value)
    {
        value = ReadOnlySpan<byte>.Empty;
        if (!TryReadVarint(data, ref offset, out var length) || length < 0 || offset + length > data.Length)
        {
            return false;
        }

        value = data.Slice(offset, length);
        offset += length;
        return true;
    }

    private static string? ReadString(ReadOnlySpan<byte> data, ref int offset)
    {
        if (!TryReadLengthDelimited(data, ref offset, out var bytes))
        {
            return null;
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static int ReadVarint(ReadOnlySpan<byte> data, ref int offset)
    {
        return TryReadVarint(data, ref offset, out var value) ? value : 0;
    }

    private static bool TryReadVarint(ReadOnlySpan<byte> data, ref int offset, out int value)
    {
        value = 0;
        var shift = 0;
        while (offset < data.Length)
        {
            var b = data[offset++];
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return true;
            }

            shift += 7;
            if (shift > 35)
            {
                return false;
            }
        }

        return false;
    }

    private static bool SkipField(ReadOnlySpan<byte> data, ref int offset, int wireType)
    {
        switch (wireType)
        {
            case 0:
                return TryReadVarint(data, ref offset, out _);
            case 1:
                offset += 8;
                return offset <= data.Length;
            case 2:
                return TryReadLengthDelimited(data, ref offset, out _);
            case 5:
                offset += 4;
                return offset <= data.Length;
            default:
                return false;
        }
    }

    internal sealed class QueryAnswer
    {
        public string? Name { get; set; }
        public string? Motd { get; set; }
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
        public bool HasPassword { get; set; }
        public string? ServerVersion { get; set; }
    }
}
