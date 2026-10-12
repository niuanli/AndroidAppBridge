namespace AndroidAppBridge.Infrastructure;

public sealed class FileUserPreferences(string folder) : IUserPreferences
{
    public bool English
    {
        get
        {
            try
            {
                return File.ReadAllText(Path.Combine(folder, "language.txt")).Trim() == "EN";
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
        set
        {
            try
            {
                File.WriteAllText(Path.Combine(folder, "language.txt"), value ? "EN" : "RU");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
