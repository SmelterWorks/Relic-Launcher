using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Modpacks;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ModOriginResolverTests
{
    [Fact]
    public void Resolve_ModDbZipFilename_ReturnsFileId()
    {
        using var temp = new TempAppPaths();
        var resolver = new ModOriginResolver(new FixedPathProvider(temp.Paths));
        var mod = new LocalModInfo
        {
            Path = "/mods/mod_42.zip",
            FileName = "mod_42.zip",
            ModId = "carrycapacity",
            IsEnabled = true,
        };

        var origin = resolver.Resolve(mod);
        origin.Source.Should().Be(ModpackModSource.ModDb);
        origin.FileId.Should().Be(42);
    }

    [Fact]
    public void Resolve_IndexMatch_ReturnsFileId()
    {
        using var temp = new TempAppPaths();
        var indexPath = Path.Combine(temp.Paths.CacheDirectory, "mods", "files", "index.json");
        Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
        File.WriteAllText(indexPath, """{"99":{"fileId":99,"fileName":"custom.zip","modId":"carrycapacity"}}""");

        var resolver = new ModOriginResolver(new FixedPathProvider(temp.Paths));
        var mod = new LocalModInfo
        {
            Path = "/mods/custom.zip",
            FileName = "custom.zip",
            ModId = "carrycapacity",
            IsEnabled = true,
        };

        var origin = resolver.Resolve(mod);
        origin.Source.Should().Be(ModpackModSource.ModDb);
        origin.FileId.Should().Be(99);
    }

    [Fact]
    public void Resolve_Directory_ReturnsLocal()
    {
        using var temp = new TempAppPaths();
        var resolver = new ModOriginResolver(new FixedPathProvider(temp.Paths));
        var mod = new LocalModInfo
        {
            Path = "/mods/mydevmod",
            FileName = "mydevmod",
            ModId = "mydevmod",
            IsDirectory = true,
            IsEnabled = true,
        };

        var origin = resolver.Resolve(mod);
        origin.Source.Should().Be(ModpackModSource.Local);
        origin.FileId.Should().Be(0);
    }
}
