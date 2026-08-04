using RelicLauncher.Core.Models;

namespace RelicLauncher.Core.Abstractions;

public interface IAppPathProvider
{
    AppPaths GetPaths();
}
