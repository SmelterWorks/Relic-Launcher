using RelicLauncher.Core.Models;

namespace RelicLauncher.Core.Mods;

public static class ModDependencyResolver
{
    public const int DefaultMaxDepth = 32;

    public static ModDependencyAudit Audit(
        IReadOnlyList<LocalModInfo> installed,
        string? activeGameVersion = null,
        int maxDepth = DefaultMaxDepth)
    {
        ArgumentNullException.ThrowIfNull(installed);
        if (maxDepth < 1)
        {
            maxDepth = DefaultMaxDepth;
        }

        var byId = BuildIndex(installed);
        var issues = new List<ModDependencyIssue>();
        var missing = new Dictionary<string, ModDependencyRequirement>(StringComparer.OrdinalIgnoreCase);
        var visitedEdges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in installed)
        {
            if (string.IsNullOrWhiteSpace(mod.ModId))
            {
                continue;
            }

            Walk(
                mod.ModId!,
                mod.Dependencies,
                byId,
                activeGameVersion,
                maxDepth,
                path: [mod.ModId!],
                issues,
                missing,
                visitedEdges);
        }

        return BuildAudit(issues, missing);
    }

    public static ModDependencyAudit AuditMod(
        LocalModInfo root,
        IReadOnlyList<LocalModInfo> installed,
        string? activeGameVersion = null,
        int maxDepth = DefaultMaxDepth)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(installed);
        if (string.IsNullOrWhiteSpace(root.ModId))
        {
            return new ModDependencyAudit();
        }

        if (maxDepth < 1)
        {
            maxDepth = DefaultMaxDepth;
        }

        var byId = BuildIndex(installed);
        var issues = new List<ModDependencyIssue>();
        var missing = new Dictionary<string, ModDependencyRequirement>(StringComparer.OrdinalIgnoreCase);
        var visitedEdges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Walk(
            root.ModId!,
            root.Dependencies,
            byId,
            activeGameVersion,
            maxDepth,
            path: [root.ModId!],
            issues,
            missing,
            visitedEdges);

        return BuildAudit(issues, missing);
    }

    private static void Walk(
        string dependentModId,
        IReadOnlyList<ModDependencyRequirement> dependencies,
        Dictionary<string, LocalModInfo> byId,
        string? activeGameVersion,
        int maxDepth,
        List<string> path,
        List<ModDependencyIssue> issues,
        Dictionary<string, ModDependencyRequirement> missing,
        HashSet<string> visitedEdges)
    {
        if (path.Count > maxDepth)
        {
            return;
        }

        foreach (var dep in dependencies)
        {
            ClassifyDependency(
                dependentModId,
                dep,
                byId,
                activeGameVersion,
                maxDepth,
                path,
                issues,
                missing,
                visitedEdges);
        }
    }

    private static void ClassifyDependency(
        string dependentModId,
        ModDependencyRequirement dep,
        Dictionary<string, LocalModInfo> byId,
        string? activeGameVersion,
        int maxDepth,
        List<string> path,
        List<ModDependencyIssue> issues,
        Dictionary<string, ModDependencyRequirement> missing,
        HashSet<string> visitedEdges)
    {
        if (string.IsNullOrWhiteSpace(dep.ModId))
        {
            return;
        }

        var requiredId = dep.ModId.Trim();
        if (!visitedEdges.Add($"{dependentModId}>{requiredId}"))
        {
            return;
        }

        if (TryAddCycleIssue(dependentModId, dep, requiredId, path, issues))
        {
            return;
        }

        if (BuiltinModIds.IsBuiltin(requiredId))
        {
            ClassifyBuiltin(dependentModId, dep, activeGameVersion, issues);
            return;
        }

        if (!byId.TryGetValue(requiredId, out var installed))
        {
            issues.Add(CreateIssue(dependentModId, dep, null, ModDependencyIssueKind.Missing));
            MergeMissing(missing, dep);
            return;
        }

        if (!TryClassifyInstalled(dependentModId, dep, installed, issues, missing))
        {
            return;
        }

        Walk(
            requiredId,
            installed.Dependencies,
            byId,
            activeGameVersion,
            maxDepth,
            [.. path, requiredId],
            issues,
            missing,
            visitedEdges);
    }

    private static bool TryAddCycleIssue(
        string dependentModId,
        ModDependencyRequirement dep,
        string requiredId,
        List<string> path,
        List<ModDependencyIssue> issues)
    {
        var cycleIndex = path.FindIndex(p => string.Equals(p, requiredId, StringComparison.OrdinalIgnoreCase));
        if (cycleIndex < 0)
        {
            return false;
        }

        issues.Add(new ModDependencyIssue
        {
            DependentModId = dependentModId,
            RequiredModId = requiredId,
            RequiredMinimumVersion = dep.MinimumVersion,
            Kind = ModDependencyIssueKind.Cycle,
            CyclePath = path.Skip(cycleIndex).Append(requiredId).ToList(),
        });
        return true;
    }

    private static bool TryClassifyInstalled(
        string dependentModId,
        ModDependencyRequirement dep,
        LocalModInfo installed,
        List<ModDependencyIssue> issues,
        Dictionary<string, ModDependencyRequirement> missing)
    {
        if (!installed.IsEnabled)
        {
            issues.Add(CreateIssue(dependentModId, dep, installed.Version, ModDependencyIssueKind.Disabled));
            return false;
        }

        if (!ModVersionComparer.Satisfies(installed.Version, dep.MinimumVersion))
        {
            issues.Add(CreateIssue(dependentModId, dep, installed.Version, ModDependencyIssueKind.Outdated));
            MergeMissing(missing, dep);
            return false;
        }

        issues.Add(CreateIssue(dependentModId, dep, installed.Version, ModDependencyIssueKind.Satisfied));
        return true;
    }

    private static ModDependencyIssue CreateIssue(
        string dependentModId,
        ModDependencyRequirement dep,
        string? installedVersion,
        ModDependencyIssueKind kind)
        => new()
        {
            DependentModId = dependentModId,
            RequiredModId = dep.ModId.Trim(),
            RequiredMinimumVersion = dep.MinimumVersion,
            InstalledVersion = installedVersion,
            Kind = kind,
        };

    private static void ClassifyBuiltin(
        string dependentModId,
        ModDependencyRequirement dep,
        string? activeGameVersion,
        List<ModDependencyIssue> issues)
    {
        var requiredId = dep.ModId.Trim();
        if (string.Equals(requiredId, BuiltinModIds.Game, StringComparison.OrdinalIgnoreCase)
            && !ModVersionComparer.IsAnyVersion(dep.MinimumVersion)
            && !string.IsNullOrWhiteSpace(activeGameVersion)
            && !ModVersionComparer.Satisfies(activeGameVersion, dep.MinimumVersion))
        {
            issues.Add(new ModDependencyIssue
            {
                DependentModId = dependentModId,
                RequiredModId = requiredId,
                RequiredMinimumVersion = dep.MinimumVersion,
                InstalledVersion = activeGameVersion,
                Kind = ModDependencyIssueKind.BuiltinVersionMismatch,
            });
            return;
        }

        issues.Add(new ModDependencyIssue
        {
            DependentModId = dependentModId,
            RequiredModId = requiredId,
            RequiredMinimumVersion = dep.MinimumVersion,
            InstalledVersion = string.Equals(requiredId, BuiltinModIds.Game, StringComparison.OrdinalIgnoreCase)
                ? activeGameVersion
                : null,
            Kind = ModDependencyIssueKind.Satisfied,
        });
    }

    private static void MergeMissing(
        Dictionary<string, ModDependencyRequirement> missing,
        ModDependencyRequirement dep)
    {
        var id = dep.ModId.Trim();
        if (missing.TryGetValue(id, out var existing))
        {
            missing[id] = new ModDependencyRequirement
            {
                ModId = id,
                MinimumVersion = ModVersionComparer.TakeHigherMinimum(existing.MinimumVersion, dep.MinimumVersion),
            };
            return;
        }

        missing[id] = new ModDependencyRequirement
        {
            ModId = id,
            MinimumVersion = ModVersionComparer.IsAnyVersion(dep.MinimumVersion)
                ? null
                : dep.MinimumVersion?.Trim(),
        };
    }

    private static Dictionary<string, LocalModInfo> BuildIndex(IReadOnlyList<LocalModInfo> installed)
    {
        var map = new Dictionary<string, LocalModInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in installed)
        {
            if (string.IsNullOrWhiteSpace(mod.ModId))
            {
                continue;
            }

            if (!map.TryGetValue(mod.ModId, out var existing))
            {
                map[mod.ModId] = mod;
                continue;
            }

            if (mod.IsEnabled && !existing.IsEnabled)
            {
                map[mod.ModId] = mod;
                continue;
            }

            if (mod.IsEnabled == existing.IsEnabled
                && ModVersionComparer.Compare(mod.Version, existing.Version) > 0)
            {
                map[mod.ModId] = mod;
            }
        }

        return map;
    }

    private static ModDependencyAudit BuildAudit(
        List<ModDependencyIssue> issues,
        Dictionary<string, ModDependencyRequirement> missing)
    {
        var blocking = issues
            .Where(i => i.Kind != ModDependencyIssueKind.Satisfied)
            .ToList();

        var byDependent = blocking
            .GroupBy(i => i.DependentModId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                IReadOnlyList<ModDependencyIssue> (g) => g.ToList(),
                StringComparer.OrdinalIgnoreCase);

        return new ModDependencyAudit
        {
            Issues = issues,
            IssuesByDependentModId = byDependent,
            MissingExternalRequirements = missing.Values.ToList(),
        };
    }
}
