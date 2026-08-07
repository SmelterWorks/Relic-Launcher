using FluentAssertions;
using RelicLauncher.Infrastructure.Servers;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class VintageStoryLanQueryProtocolTests
{
    [Fact]
    public void TryParseQueryAnswer_ReadsNameAndPlayers()
    {
        var payload = BuildServerQueryAnswerPacket(new VintageStoryLanQueryProtocol.QueryAnswer
        {
            Name = "Test LAN World",
            Motd = "Welcome",
            PlayerCount = 3,
            MaxPlayers = 8,
            HasPassword = true,
            ServerVersion = "1.22.6",
        });

        VintageStoryLanQueryProtocol.TryParseQueryAnswer(payload, out var answer).Should().BeTrue();
        answer.Name.Should().Be("Test LAN World");
        answer.Motd.Should().Be("Welcome");
        answer.PlayerCount.Should().Be(3);
        answer.MaxPlayers.Should().Be(8);
        answer.HasPassword.Should().BeTrue();
        answer.ServerVersion.Should().Be("1.22.6");
    }

    private static byte[] BuildServerQueryAnswerPacket(VintageStoryLanQueryProtocol.QueryAnswer answer)
    {
        var nested = EncodeQueryAnswer(answer);
        var server = EncodeLengthDelimitedField(28, nested);
        var length = new byte[] { (byte)server.Length };
        return length.Concat(server).ToArray();
    }

    private static byte[] EncodeQueryAnswer(VintageStoryLanQueryProtocol.QueryAnswer answer)
    {
        var parts = new List<byte[]>();
        if (answer.Name is not null)
        {
            parts.Add(EncodeStringField(1, answer.Name));
        }

        if (answer.Motd is not null)
        {
            parts.Add(EncodeStringField(2, answer.Motd));
        }

        parts.Add(EncodeVarintField(3, answer.PlayerCount));
        parts.Add(EncodeVarintField(4, answer.MaxPlayers));
        if (answer.HasPassword)
        {
            parts.Add(EncodeVarintField(6, 1));
        }

        if (answer.ServerVersion is not null)
        {
            parts.Add(EncodeStringField(7, answer.ServerVersion));
        }

        return parts.SelectMany(static p => p).ToArray();
    }

    private static byte[] EncodeStringField(int fieldNumber, string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var tag = EncodeVarint((fieldNumber << 3) | 2);
        var length = EncodeVarint(bytes.Length);
        return tag.Concat(length).Concat(bytes).ToArray();
    }

    private static byte[] EncodeVarintField(int fieldNumber, int value)
    {
        var tag = EncodeVarint((fieldNumber << 3) | 0);
        var payload = EncodeVarint(value);
        return tag.Concat(payload).ToArray();
    }

    private static byte[] EncodeLengthDelimitedField(int fieldNumber, byte[] payload)
    {
        var tag = EncodeVarint((fieldNumber << 3) | 2);
        var length = EncodeVarint(payload.Length);
        return tag.Concat(length).Concat(payload).ToArray();
    }

    private static byte[] EncodeVarint(int value)
    {
        var bytes = new List<byte>(5);
        var unsigned = (uint)value;
        while (unsigned >= 0x80)
        {
            bytes.Add((byte)(unsigned | 0x80));
            unsigned >>= 7;
        }

        bytes.Add((byte)unsigned);
        return bytes.ToArray();
    }
}
