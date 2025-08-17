using System.Security.Cryptography;
using System.Text;

namespace RfcBuddy.App.Core;

/// <summary>
/// Helps with various crypto functions
/// </summary>
internal static class Cryptography
{
    /// <summary>
    /// Calculates an SHA256 hash of a given string.
    /// </summary>
    /// <remarks>
    /// Based on https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256
    /// </remarks>
    /// <param name="input">The string to hash</param>
    /// <returns>An SHA256 hash of the input string</returns>
    internal static string GetSha256Hash(string input)
    {
        StringBuilder sBuilder = new();
        byte[] data = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        for (int i = 0; i < data.Length; i++)
        {
            sBuilder.Append(data[i].ToString("x2"));  //convert bytes to hex characters
        }
        return sBuilder.ToString();
    }

    /// <summary>
    /// Verify whether a string matches a given SHA256 hash.
    /// </summary>
    /// <remarks>
    /// Based on https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256
    /// </remarks>
    /// <param name="input">The string to verify</param>
    /// <param name="sha256hash">The SHA256 hash to verify against</param>
    /// <returns>True, if the SHA256 hash of the string matches the given hash. False otherwise.</returns>
    internal static bool VerifySha256Hash(string input, string sha256hash)
    {
        string hashOfInput = GetSha256Hash(input);
        StringComparer comparer = StringComparer.OrdinalIgnoreCase;
        return (0 == comparer.Compare(hashOfInput, sha256hash));
    }
}
