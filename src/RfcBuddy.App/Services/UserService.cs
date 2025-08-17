using RfcBuddy.App.Core;
using RfcBuddy.App.Objects;
using System.Security.Principal;

namespace RfcBuddy.App.Services;

/// <summary>
/// Service to store and rerieve user-specific settings
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Loads the previously reviewed RFCs into a list.
    /// </summary>
    /// <returns>A list of the previously reviewed RFCs. If none are found, an empty list is returned.</returns>
    public List<PreviousRfc> GetPreviousRfcs();

    /// <summary>
    /// Saves the given RFCs, with a hash of the dates, asset tags, description, and risk assessment for later comparison.
    /// </summary>
    /// <param name="rfcs">The RFCs to save.</param>
    public void SavePreviousRfcs(IEnumerable<Rfc> rfcs);

    /// <summary>
    /// Gets the current user's keywords.
    /// </summary>
    /// <param name="ministryKeywords">The ministry-specific keywords.</param>
    /// <param name="generalKeywords">The general keywords.</param>
    /// <param name="ignoreKeywords">The keywords to ignore.</param>
    public void GetUserKeywords(out List<string> ministryKeywords, out List<string> generalKeywords, out List<string> ignoreKeywords);

    /// <summary>
    /// Saves the current user's keywords.
    /// </summary>
    /// <param name="ministryKeywords">The ministry-specific keywords.</param>
    /// <param name="generalKeywords">The general keywords.</param>
    /// <param name="ignoreKeywords">The keywords to ignore.</param>
    public void SaveUserKeywords(List<string> ministryKeywords, List<string> generalKeywords, List<string> ignoreKeywords);
}

/// <summary>
/// Implementation of IUserService, which stores user data in the file system
/// </summary>
/// <param name="appSettingsService">The app settings, including the data folder location</param>
public class UserService(IAppSettingsService appSettingsService, IPrincipal user) : IUserService
{
    /// <summary>
    /// The app settings object.
    /// </summary>
    private readonly AppSettings _appSettings = appSettingsService.AppSettings;

    /// <summary>
    /// The current user. This is a ClaimsPrincipal at runtime.
    /// </summary>
    private readonly IPrincipal _user = user;

    private string UserId
    {
        get
        {
            return Cryptography.GetSha256Hash(_user.Identity?.Name ?? "Generic User");
        }
    }

    #region Previous RFCs
    /// <summary>
    /// A separator used to delimit fields in the previousRfcs file.
    /// </summary>
    private const char previousRfcSeparator = '#';

    /// <summary>
    /// The date format to keep track of previous RFCs
    /// </summary>
    private const string previousRfcDateFormat = "yyyy-MM-dd-HHmmss";

    /// <summary>
    /// The filename to use for storing previous RFCs per user
    /// </summary>
    private const string previousRfcsFileName = "PreviousRFCs.txt";

    /// <summary>
    /// The relative path to the previous RFCs file.
    /// </summary>
    private string PreviousRfcsFilePath => Path.Combine(_appSettings.DataFolder, UserId);

    /// <summary>
    /// The relative path to the previous RFCs file, including the filename.
    /// </summary>
    private string PreviousRfcsFile => Path.Combine(PreviousRfcsFilePath, previousRfcsFileName);

    /// <summary>
    /// Loads the previously reviewed RFCs into a list.
    /// </summary>
    /// <returns>A list of the previously reviewed RFCs. If none are found, an empty list is returned.</returns>
    public List<PreviousRfc> GetPreviousRfcs()
    {
        List<PreviousRfc> result = [];
        if (File.Exists(PreviousRfcsFile))
        {
            using StreamReader previousRfcs = File.OpenText(PreviousRfcsFile);
            while (!previousRfcs.EndOfStream)
            {
                string? previousRfc = previousRfcs.ReadLine();
                if (null != previousRfc)
                {
                    string[] rfcElements = previousRfc.Split(previousRfcSeparator);
                    if (6 == rfcElements.Length)  //Very basic check whether the line is valid - it contains 6 elements.
                    {
                        result.Add(new PreviousRfc(rfcElements[0])
                        {
                            StartDate = DateTime.ParseExact(rfcElements[1], previousRfcDateFormat, System.Globalization.CultureInfo.InvariantCulture),
                            EndDate = DateTime.ParseExact(rfcElements[2], previousRfcDateFormat, System.Globalization.CultureInfo.InvariantCulture),
                            AssetTagsHash = rfcElements[3],
                            DescriptionHash = rfcElements[4],
                            RiskAssessmentHash = rfcElements[5]
                        });
                    }
                }
            }
            previousRfcs.Close();
        }
        return result;
    }

    /// <summary>
    /// Saves the given RFCs to a file, with a hash of the dates, asset tags, description, and risk assessment for later comparison.
    /// </summary>
    /// <param name="rfcs">The RFCs to write to the file.</param>
    public void SavePreviousRfcs(IEnumerable<Rfc> rfcs)
    {
        if (!Directory.Exists(PreviousRfcsFilePath))
        {
            Directory.CreateDirectory(PreviousRfcsFilePath);
        }
        if (!File.Exists(PreviousRfcsFile))
        {
            using FileStream tmp = File.Create(PreviousRfcsFile);
            tmp.Close();
        }
        using FileStream previousRfcsFileStream = File.Open(PreviousRfcsFile, FileMode.Truncate);
        using (StreamWriter previousRfcs = new(previousRfcsFileStream))
        {
            foreach (Rfc rfc in rfcs)
            {
                previousRfcs.WriteLine(rfc.RfcNumber.ToString()
                    + previousRfcSeparator + rfc.StartDate.ToString(previousRfcDateFormat)
                    + previousRfcSeparator + rfc.EndDate.ToString(previousRfcDateFormat)
                    + previousRfcSeparator + Cryptography.GetSha256Hash(rfc.AssetTags)
                    + previousRfcSeparator + Cryptography.GetSha256Hash(rfc.Description)
                    + previousRfcSeparator + Cryptography.GetSha256Hash(rfc.RiskAssessment));
            }
            previousRfcs.Close();
        }
        previousRfcsFileStream.Close();
    }
    #endregion

    #region User Keywords
    /// <summary>
    /// Keywords are comma-separated when the user enters them. It should be safe to use the same character here.
    /// </summary>
    private const char keywordSeparator = ',';

    /// <summary>
    /// The filename to use for storing keywords per user
    /// </summary>
    private const string keywordsFileName = "Keywords.txt";

    /// <summary>
    /// The relative path to the keywords file.
    /// </summary>
    private string KeywordsFilePath => Path.Combine(_appSettings.DataFolder, UserId);

    /// <summary>
    /// The relative path to the keywords file, including the filename.
    /// </summary>
    private string KeywordsFile => Path.Combine(KeywordsFilePath, keywordsFileName);

    /// <summary>
    /// Turns the character-separated keywords into a list.
    /// </summary>
    /// <param name="keywords">The character-separated list of keywords.</param>
    /// <returns>A list with all the keywords found.</returns>
    internal static List<string> ParseKeywords(string? keywords)
    {
        if (!string.IsNullOrEmpty(keywords))
        {
            return [.. keywords.Split(keywordSeparator)];
        }
        return [];
    }

    /// <summary>
    /// Gets the current user's keywords.
    /// </summary>
    /// <param name="ministryKeywords">The ministry-specific keywords.</param>
    /// <param name="generalKeywords">The general keywords.</param>
    /// <param name="ignoreKeywords">The keywords to ignore.</param>
    public void GetUserKeywords(out List<string> ministryKeywords, out List<string> generalKeywords, out List<string> ignoreKeywords)
    {
        ministryKeywords = [];
        generalKeywords = [];
        ignoreKeywords = [];
        if (File.Exists(KeywordsFile))
        {
            using StreamReader keywordsFileStream = File.OpenText(KeywordsFile);
            ministryKeywords = ParseKeywords(keywordsFileStream.ReadLine());
            generalKeywords = ParseKeywords(keywordsFileStream.ReadLine());
            ignoreKeywords = ParseKeywords(keywordsFileStream.ReadLine());
            keywordsFileStream.Close();
        }
    }

    /// <summary>
    /// Saves the current user's keywords to a file.
    /// </summary>
    /// <param name="ministryKeywords">The ministry-specific keywords.</param>
    /// <param name="generalKeywords">The general keywords.</param>
    /// <param name="ignoreKeywords">The keywords to ignore.</param>
    public void SaveUserKeywords(List<string> ministryKeywords, List<string> generalKeywords, List<string> ignoreKeywords)
    {
        if (!Directory.Exists(KeywordsFilePath))
        {
            Directory.CreateDirectory(KeywordsFilePath);
        }
        if (!File.Exists(KeywordsFile))
        {
            using FileStream tmp = File.Create(KeywordsFile);
            tmp.Close();
        }
        using FileStream keywordsFileStream = File.Open(KeywordsFile, FileMode.Truncate);
        using (StreamWriter keywords = new(keywordsFileStream))
        {
            keywords.WriteLine(string.Join(keywordSeparator, ministryKeywords));
            keywords.WriteLine(string.Join(keywordSeparator, generalKeywords));
            keywords.WriteLine(string.Join(keywordSeparator, ignoreKeywords));
            keywords.Close();
        }
        keywordsFileStream.Close();
    }
    #endregion
}
