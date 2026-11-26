namespace Wharf.State;

internal static class AtomicFile
{
    public static void Replace(string path, string content)
    {
        Write(path, content, overwrite: true);
    }

    public static bool TryCreate(string path, string content) =>
        Write(path, content, overwrite: false);

    private static bool Write(string path, string content, bool overwrite)
    {
        string temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, content);
            try
            {
                File.Move(temporary, path, overwrite);
                return true;
            }
            catch (IOException) when (!overwrite && File.Exists(path))
            {
                return false;
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
