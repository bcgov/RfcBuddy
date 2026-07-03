using System.Reflection;

namespace RfcBuddy.Web.Support;

public static class AppVersion
{
    public static string Current => GetCurrentVersion();

    private static string GetCurrentVersion()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return "unknown";
        }

        int plusIndex = informationalVersion.IndexOf('+');
        return plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;
    }
}
