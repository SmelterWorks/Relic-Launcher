using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Mods;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class ModDependencyResolverTests
{
    [Fact]
    public void Audit_ReportsMissingDependency()
    {
        var mods = new[]
        {
            Mod("root", "1.0.0", deps: [Dep("lib", "1.0.0")]),
        };

        var audit = ModDependencyResolver.Audit(mods);

        audit.HasBlockingIssues.Should().BeTrue();
        audit.Issues.Should().Contain(i =>
            i.RequiredModId == "lib" && i.Kind == ModDependencyIssueKind.Missing);
        audit.MissingExternalRequirements.Should().ContainSingle(r => r.ModId == "lib");
    }

    [Fact]
    public void Audit_Diamond_TakesHighestMinimum()
    {
        var mods = new[]
        {
            Mod("a", "1.0.0", deps: [Dep("lib", "1.0.0"), Dep("b", "1.0.0")]),
            Mod("b", "1.0.0", deps: [Dep("lib", "2.0.0")]),
        };

        var audit = ModDependencyResolver.Audit(mods);

        audit.MissingExternalRequirements.Should().ContainSingle(r =>
            r.ModId == "lib" && r.MinimumVersion == "2.0.0");
    }

    [Fact]
    public void Audit_DisabledDependency_IsDisabled()
    {
        var mods = new[]
        {
            Mod("root", "1.0.0", deps: [Dep("lib", "1.0.0")]),
            Mod("lib", "1.0.0", enabled: false),
        };

        var audit = ModDependencyResolver.Audit(mods);

        audit.Issues.Should().Contain(i =>
            i.RequiredModId == "lib" && i.Kind == ModDependencyIssueKind.Disabled);
    }

    [Fact]
    public void Audit_OutdatedDependency()
    {
        var mods = new[]
        {
            Mod("root", "1.0.0", deps: [Dep("lib", "2.0.0")]),
            Mod("lib", "1.5.0"),
        };

        var audit = ModDependencyResolver.Audit(mods);

        audit.Issues.Should().Contain(i =>
            i.RequiredModId == "lib" && i.Kind == ModDependencyIssueKind.Outdated);
    }

    [Fact]
    public void Audit_SatisfiedAndBuiltinGame()
    {
        var mods = new[]
        {
            Mod("root", "1.0.0", deps: [Dep("game", "1.20.0"), Dep("lib", "*")]),
            Mod("lib", "3.0.0"),
        };

        var audit = ModDependencyResolver.Audit(mods, activeGameVersion: "1.22.0");

        audit.HasBlockingIssues.Should().BeFalse();
        audit.Issues.Should().OnlyContain(i => i.Kind == ModDependencyIssueKind.Satisfied);
    }

    [Fact]
    public void Audit_BuiltinGameTooOld()
    {
        var mods = new[]
        {
            Mod("root", "1.0.0", deps: [Dep("game", "1.22.0")]),
        };

        var audit = ModDependencyResolver.Audit(mods, activeGameVersion: "1.20.0");

        audit.Issues.Should().Contain(i =>
            i.RequiredModId == "game" && i.Kind == ModDependencyIssueKind.BuiltinVersionMismatch);
    }

    [Fact]
    public void Audit_DetectsCycle()
    {
        var mods = new[]
        {
            Mod("a", "1.0.0", deps: [Dep("b", "*")]),
            Mod("b", "1.0.0", deps: [Dep("a", "*")]),
        };

        var audit = ModDependencyResolver.Audit(mods);

        audit.Issues.Should().Contain(i => i.Kind == ModDependencyIssueKind.Cycle);
    }

    [Fact]
    public void Audit_RespectsDepthCap()
    {
        var mods = new[]
        {
            Mod("a", "1.0.0", deps: [Dep("missing", "1.0.0")]),
        };

        var audit = ModDependencyResolver.Audit(mods, maxDepth: 1);
        audit.Issues.Should().NotBeEmpty();
    }

    [Fact]
    public void AuditMod_ScopesToSingleRoot()
    {
        var mods = new[]
        {
            Mod("a", "1.0.0", deps: [Dep("missinga", "*")]),
            Mod("b", "1.0.0", deps: [Dep("missingb", "*")]),
        };

        var audit = ModDependencyResolver.AuditMod(mods[0], mods);

        audit.Issues.Should().Contain(i => i.RequiredModId == "missinga");
        audit.Issues.Should().NotContain(i => i.RequiredModId == "missingb");
    }

    private static LocalModInfo Mod(
        string id,
        string version,
        bool enabled = true,
        ModDependencyRequirement[]? deps = null)
        => new()
        {
            Path = $"/mods/{id}.zip",
            FileName = $"{id}.zip",
            ModId = id,
            Name = id,
            Version = version,
            IsEnabled = enabled,
            Dependencies = deps ?? [],
        };

    private static ModDependencyRequirement Dep(string id, string? minimum)
        => new() { ModId = id, MinimumVersion = minimum };
}
