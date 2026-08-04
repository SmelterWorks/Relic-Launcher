using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IFileExplorerService
{
    Result OpenFolder(string folderPath);
}
