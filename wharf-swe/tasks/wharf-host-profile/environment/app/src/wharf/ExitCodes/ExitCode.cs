namespace Wharf.ExitCodes;

public readonly record struct ExitCode(int Value)
{
    public static ExitCode Success => new(0);
    public static ExitCode InternalFailure => new(1);
    public static ExitCode InvalidInput => new(2);
    public static ExitCode UnsupportedHost => new(3);
    public static ExitCode BlockedRun => new(4);
    public static ExitCode PartialRun => new(5);
}
