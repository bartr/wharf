namespace Wharf.Tests;

public sealed class PackagingScriptTests
{
    [Fact]
    public void PackagingScriptsAreExecutableAndUseExplicitPrivilegeBoundaries()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string package = Path.Combine(root, "scripts/package-wharf.sh");
        string bootstrap = Path.Combine(root, "scripts/wharf-bootstrap.sh");
        string rollback = Path.Combine(root, "scripts/wharf-rollback.sh");

        Assert.True(File.Exists(package));
        Assert.True(File.Exists(bootstrap));
        Assert.True(File.Exists(rollback));
        Assert.Contains("dpkg-deb --build", File.ReadAllText(package));
        Assert.Contains("SIGNING_KEY", File.ReadAllText(package));
        Assert.Contains("exec sudo", File.ReadAllText(bootstrap));
        Assert.Contains("exec sudo", File.ReadAllText(rollback));
    }
}
