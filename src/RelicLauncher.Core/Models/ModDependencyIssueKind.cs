namespace RelicLauncher.Core.Models;

public enum ModDependencyIssueKind
{
    Satisfied = 0,
    Missing = 1,
    Disabled = 2,
    Outdated = 3,
    BuiltinVersionMismatch = 4,
    Unresolved = 5,
    Cycle = 6,
}
