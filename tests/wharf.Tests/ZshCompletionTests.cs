using System.Diagnostics;
using Wharf.Configuration;

namespace Wharf.Tests;

public sealed class ZshCompletionTests
{
    [Theory]
    [InlineData("completions")]
    [InlineData("completion")]
    public async Task PrintsSourceableScriptForBothCommandSpellings(string command)
    {
        var result = await WharfAppTests.InvokeWithUserConfig(null, command, "zsh");
        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("#compdef wharf\n", result.Output);
        Assert.Empty(result.Error);
        var syntax = await InvokeZsh(result.Output, syntaxOnly: true);
        Assert.Equal(0, syntax.ExitCode);
        Assert.Empty(syntax.Error);
    }

    [Theory]
    [InlineData("bash")]
    [InlineData("fish")]
    public async Task UnsupportedShellFailsWithoutPrintingAScript(string shell)
    {
        var result = await WharfAppTests.InvokeWithUserConfig(null, "completions", shell);
        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("only zsh", result.Error);
    }

    [Theory]
    [InlineData("wharf ru", "run")]
    [InlineData("wharf analytics ex", "export")]
    [InlineData("wharf analytics export --rec", "--recompute")]
    [InlineData("wharf run --cli-v", "--cli-version")]
    [InlineData("wharf completions zs", "zsh")]
    public async Task FunctionQueriesCurrentCommandTree(string buffer, string expected)
    {
        var script = await WharfAppTests.InvokeWithUserConfig(null, "completions", "zsh");
        var result = await InvokeZsh(script.Output, buffer);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Equal(expected, result.Output.Trim());
    }

    [Fact]
    public async Task CursorStopsBeforeLaterArgumentsAndHandlesSupplementaryUnicode()
    {
        var script = await WharfAppTests.InvokeWithUserConfig(null, "completions", "zsh");
        string prefix = "wharf run --include '\U0001f600' --o";
        var result = await InvokeZsh(script.Output, prefix + " --model later",
            prefix.EnumerateRunes().Count());
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Equal(["--oracle", "--output-directory", "--output-format"],
            result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task FallsBackToShellFileCompletionWithoutParserSuggestions()
    {
        var script = await WharfAppTests.InvokeWithUserConfig(null, "completions", "zsh");
        var result = await InvokeZsh(script.Output, "wharf run --tasks ./missing-prefix");
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Equal("FILES", result.Output.Trim());
    }

    [Fact]
    public async Task QueryFailurePreservesErrorAndDoesNotOfferFiles()
    {
        var script = await WharfAppTests.InvokeWithUserConfig(null, "completions", "zsh");
        var result = await InvokeZsh(script.Output, "wharf ru", failQuery: true);
        Assert.Equal(7, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("completion query failed", result.Error);
    }

    private static async Task<(int ExitCode, string Output, string Error)> InvokeZsh(
        string completionScript, string buffer = "", int? cursor = null,
        bool syntaxOnly = false, bool failQuery = false)
    {
        string home = Path.Combine(Path.GetTempPath(), $"wharf-zsh-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        try
        {
            string script = Path.Combine(home, "_wharf");
            File.WriteAllText(script, completionScript);
            string executable = Path.Combine(home, "wharf");
            File.WriteAllText(executable, failQuery
                ? "#!/bin/sh\nprintf 'completion query failed\\n' >&2\nexit 7\n"
                : "#!/bin/sh\nexec dotnet \"$WHARF_TEST_ASSEMBLY\" \"$@\"\n");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("zsh")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = home
                }
            };
            process.StartInfo.Environment["HOME"] = home;
            process.StartInfo.Environment["PATH"] = home + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
            process.StartInfo.Environment["WHARF_TEST_ASSEMBLY"] = typeof(WharfApp).Assembly.Location;
            foreach (string name in RunConfigurationResolver.ReadEnvironment().Keys)
                process.StartInfo.Environment.Remove(name);
            if (syntaxOnly)
            {
                process.StartInfo.ArgumentList.Add("-n");
                process.StartInfo.ArgumentList.Add(script);
            }
            else
            {
                process.StartInfo.ArgumentList.Add("-fc");
                process.StartInfo.ArgumentList.Add("""
                    autoload -Uz compinit
                    compinit -D
                    source "$1"
                    _describe() { print -rl -- "${suggestions[@]}"; }
                    _files() { print -r -- FILES; }
                    BUFFER=$2
                    CURSOR=${3:-${#BUFFER}}
                    _wharf
                    """);
                process.StartInfo.ArgumentList.Add("wharf-completion-test");
                process.StartInfo.ArgumentList.Add(script);
                process.StartInfo.ArgumentList.Add(buffer);
                if (cursor is not null)
                    process.StartInfo.ArgumentList.Add(cursor.Value.ToString());
            }
            Assert.True(process.Start());
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.False(Directory.Exists(Path.Combine(home, "jobs")));
            return (process.ExitCode, await output, await error);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }
}
