using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IUrlLauncher
{
    Result OpenUrl(string url);
}
