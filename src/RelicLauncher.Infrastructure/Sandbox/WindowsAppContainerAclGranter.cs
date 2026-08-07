using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Infrastructure.Sandbox;

public sealed class WindowsAppContainerAclGranter
{
    private readonly ILogger<WindowsAppContainerAclGranter> _logger;

    public WindowsAppContainerAclGranter(ILogger<WindowsAppContainerAclGranter> logger)
    {
        _logger = logger;
    }

    public Task<Result> GrantPolicyPathsAsync(
        IntPtr appContainerSid,
        IReadOnlyList<PathGrant> grants,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(Result.Success());
        }

        try
        {
            var identity = new SecurityIdentifier(appContainerSid);
            foreach (var grant in grants)
            {
                if (string.IsNullOrWhiteSpace(grant.Path))
                {
                    continue;
                }

                if (!Directory.Exists(grant.Path) && !File.Exists(grant.Path))
                {
                    try
                    {
                        Directory.CreateDirectory(grant.Path);
                    }
                    catch
                    {
                        if (!Directory.Exists(grant.Path))
                        {
                            continue;
                        }
                    }
                }

                var rights = grant.Access switch
                {
                    PathAccess.ReadWrite => FileSystemRights.Modify | FileSystemRights.Read | FileSystemRights.ExecuteFile,
                    PathAccess.ReadExecute => FileSystemRights.ReadAndExecute,
                    _ => FileSystemRights.Read,
                };

                GrantDirectory(identity, grant.Path, rights);
            }

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SystemException)
        {
            _logger.LogWarning(ex, "Failed to grant AppContainer ACLs");
            return Task.FromResult(Result.Failure(ex.Message));
        }
    }

    private static void GrantDirectory(IdentityReference identity, string path, FileSystemRights rights)
    {
        var info = new DirectoryInfo(path);
        var acl = info.GetAccessControl();
        var rule = new FileSystemAccessRule(
            identity,
            rights,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow);
        acl.AddAccessRule(rule);
        info.SetAccessControl(acl);
    }
}
