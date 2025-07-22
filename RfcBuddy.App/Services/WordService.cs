using RfcBuddy.App.Core;
using RfcBuddy.App.Objects;
using System.Text.RegularExpressions;
using Xceed.Drawing;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace RfcBuddy.App.Services;

/// <summary>
/// Service to create a Word document from the given RFCs
/// </summary>
public interface IWordService
{
    /// <summary>
    /// Creates a Word file from the given RFCs.
    /// </summary>
    /// <param name="wordFile">The word file stream</param>
    /// <param name="ministryRfcs">A list of ministry-related RFCs</param>
    /// <param name="generalRfcs">A list of general RFCs</param>
    /// <param name="otherRfcs">A list of other RFCs</param>
    public void CreateWordFile(ref Stream wordFile, List<Rfc> ministryRfcs, List<Rfc> generalRfcs, List<Rfc> otherRfcs, List<PreviousRfc> previousRfcs);
}

public class WordService : IWordService
{
    //Color definitions in the Word document
    private readonly Color ministryHighlight = Color.Red;
    private readonly Color generalHighlight = Color.Purple;
    private readonly Color sundayHighlight = Color.Green;
    private readonly Color changeHighlight = Color.Blue;

    //The date format to use for start and end dates in the Word document
    private const string wordDateFormat = "ddd yyyy-MM-dd HH:mm";

    /// <summary>
    /// Creates a Word file from the given RFCs.
    /// </summary>
    /// <param name="wordFile">The word file stream</param>
    /// <param name="ministryRfcs">A list of ministry-related RFCs</param>
    /// <param name="generalRfcs">A list of general RFCs</param>
    /// <param name="otherRfcs">A list of other RFCs</param>
    public void CreateWordFile(ref Stream wordFile, List<Rfc> ministryRfcs, List<Rfc> generalRfcs, List<Rfc> otherRfcs, List<PreviousRfc> previousRfcs)
    {
        using DocX document = DocX.Create(wordFile);
        //Header and legend
        Paragraph title = document.InsertParagraph("RFCs for " + DateTime.Now.ToShortDateString());
        title.FontSize(24).Bold().UnderlineStyle(UnderlineStyle.singleLine).Color(Color.DarkBlue);
        title.Alignment = Alignment.center;
        document.InsertParagraph();
        Paragraph p1 = document.InsertParagraph("Ministry terms: ").Bold();
        p1.Append("Ministry example").Color(ministryHighlight);
        Paragraph p2 = document.InsertParagraph("General terms: ").Bold();
        p2.Append("General example").Color(generalHighlight);
        Paragraph p3 = document.InsertParagraph("Sunday change window: ").Bold();
        p3.Append("2020-01-05 06:00:00").Color(sundayHighlight);
        Paragraph p4 = document.InsertParagraph("Changed item: ").Bold();
        p4.Append("Updated Date or Description").Color(changeHighlight);

        //The following 3 categories are all divided in:
        //- In Progress
        //- New
        //- Previously Reviewed

        //Ministry RFCs
        Paragraph ministry = document.InsertParagraph("Ministry: " + ministryRfcs.Count + " RFCs");
        ministry.StyleId = "Heading1";
        ministry.Color(ministryHighlight);
        AddRfcSection(document, ministryRfcs, previousRfcs, ministryHighlight);

        //General RFCs
        Paragraph general = document.InsertParagraph("General: " + generalRfcs.Count + " RFCs");
        general.StyleId = "Heading1";
        general.Color(generalHighlight);
        AddRfcSection(document, generalRfcs, previousRfcs, generalHighlight);

        //Other RFCs
        Paragraph other = document.InsertParagraph("Other / unclassified: " + otherRfcs.Count + " RFCs");
        other.StyleId = "Heading1";
        AddRfcSection(document, otherRfcs, previousRfcs, Color.Black);

        document.Save();
    }

    /// <summary>
    /// Adds an RFC section to the document, based on a list of given RFCs.
    /// </summary>
    /// <param name="document"></param>
    /// <param name="rfcs"></param>
    /// <param name="keywordHighlight"></param>
    private void AddRfcSection(DocX document, List<Rfc> rfcs, List<PreviousRfc> previousRfcs, Color keywordHighlight)
    {
        DateTime now = DateTime.Now;
        List<string> unchangedRfcs = [];
        //Changes that are currently in progress
        Paragraph pProgress = document.InsertParagraph("In Progress");
        pProgress.StyleId = "Heading2";
        int pProgressCount = 0;
        foreach (Rfc currentRfc in rfcs.Where(x => x.StartDate <= now && x.EndDate >= now))
        {
            //Don't worry about previous RFCs - we want to see all changes that are currently in progress.
            AddRfc(document, currentRfc, null, keywordHighlight);
            document.InsertParagraph();
            document.InsertParagraph();
            pProgressCount++;
        }
        if (0 == pProgressCount)
        {
            document.InsertParagraph("No RFCs found.");
        }

        //Changes that are new or changed since the last script run
        Paragraph pNew = document.InsertParagraph("New or Changed");
        pNew.StyleId = "Heading2";
        int pNewCount = 0;
        foreach (Rfc currentRfc in rfcs.Where(x => x.StartDate > now || x.EndDate < now))
        {
            PreviousRfc? previousRfc = previousRfcs.Find(x => x.RfcNumber == currentRfc.RfcNumber);
            if (null != previousRfc
                && currentRfc.StartDate == previousRfc.StartDate
                && currentRfc.EndDate == previousRfc.EndDate
                && Cryptography.VerifySha256Hash(currentRfc.AssetTags, previousRfc.AssetTagsHash)
                && Cryptography.VerifySha256Hash(currentRfc.Description, previousRfc.DescriptionHash)
                && Cryptography.VerifySha256Hash(currentRfc.RiskAssessment, previousRfc.RiskAssessmentHash)
                )
            {
                unchangedRfcs.Add(currentRfc.RfcNumber);
            }
            else
            {
                AddRfc(document, currentRfc, previousRfc, keywordHighlight);
                document.InsertParagraph();
                document.InsertParagraph();
                pNewCount++;
            }
        }
        if (0 == pNewCount)
        {
            document.InsertParagraph("No RFCs found.");
        }

        //Changes that were identical in the previous script run
        Paragraph pPrevious = document.InsertParagraph("Previously Reviewed");
        pPrevious.StyleId = "Heading2";
        int pPreviousCount = 0;
        foreach (Rfc currentRfc in rfcs.Where(x => unchangedRfcs.Contains(x.RfcNumber)))
        {
            AddRfc(document, currentRfc, null, keywordHighlight);
            document.InsertParagraph();
            document.InsertParagraph();
            pPreviousCount++;
        }
        if (0 == pPreviousCount)
        {
            document.InsertParagraph("No RFCs found.");
        }
    }

    /// <summary>
    /// Adds the given RFC to the document
    /// </summary>
    /// <param name="document">The document to add the RFC to</param>
    /// <param name="rfc">The RFC to add</param>
    /// <param name="keywordHighlight">The colour to use for any keyword highlighting</param>
    private void AddRfc(DocX document, Rfc rfc, PreviousRfc? previousRfc, Color keywordHighlight)
    {
        Paragraph newRfc = document.InsertParagraph();
        newRfc.Append("RFC: ").Bold();
        newRfc.Append(rfc.RfcNumber.ToString());
        newRfc.Append("\tStart: ").Bold();
        if (null != previousRfc && rfc.StartDate != previousRfc.StartDate)
        {
            newRfc.Append(rfc.StartDate.ToString(wordDateFormat)).Color(changeHighlight);
        }
        else if (DayOfWeek.Sunday == rfc.StartDate.DayOfWeek && "06:00:00" == rfc.StartDate.ToString("HH:mm:ss"))
        {
            newRfc.Append(rfc.StartDate.ToString(wordDateFormat)).Color(sundayHighlight);
        }
        else
        {
            newRfc.Append(rfc.StartDate.ToString(wordDateFormat));
        }
        newRfc.Append("\t\tEnd: ").Bold();
        if (null != previousRfc && rfc.EndDate != previousRfc.EndDate)
        {
            newRfc.Append(rfc.EndDate.ToString(wordDateFormat)).Color(changeHighlight);
        }
        else if (DayOfWeek.Sunday == rfc.EndDate.DayOfWeek && "09:00:00" == rfc.EndDate.ToString("HH:mm:ss"))
        {
            newRfc.Append(rfc.EndDate.ToString(wordDateFormat)).Color(sundayHighlight);
        }
        else
        {
            newRfc.Append(rfc.EndDate.ToString(wordDateFormat));
        }
        newRfc.Append(Environment.NewLine);
        newRfc.Append("Status: ").Bold();
        newRfc.Append(rfc.ApprovalStatus);
        newRfc.Append("\tPlatform: ").Bold();
        newRfc.Append(rfc.Platform);
        newRfc.Append("\tAsset Tags: ").Bold();
        if (null == previousRfc || Cryptography.VerifySha256Hash(rfc.AssetTags, previousRfc.AssetTagsHash))
        {
            newRfc.Append(rfc.AssetTags);
        }
        else
        {
            newRfc.Append(rfc.AssetTags).Color(changeHighlight);
        }
        newRfc.Append(Environment.NewLine);
        newRfc.Append("Description: ").Bold();
        if (null == previousRfc || Cryptography.VerifySha256Hash(rfc.Description, previousRfc.DescriptionHash))
        {
            newRfc.Append(rfc.Description);
        }
        else
        {
            newRfc.Append(rfc.Description).Color(changeHighlight);
        }
        newRfc.Append(Environment.NewLine);
        newRfc.Append("Risk Assessment: ").Bold();
        if (null == previousRfc || Cryptography.VerifySha256Hash(rfc.RiskAssessment, previousRfc.RiskAssessmentHash))
        {
            newRfc.Append(rfc.RiskAssessment);
        }
        else
        {
            newRfc.Append(rfc.RiskAssessment).Color(changeHighlight);
        }
        //Highlight keywords

        foreach (string keyword in rfc.Keywords)
        {
            StringReplaceTextOptions replaceTextOptions = new()
            {
                SearchValue = @"\b" + keyword + @"\b",
                NewValue = keyword,
                TrackChanges = false,
                EscapeRegEx = false,
                RegExOptions = RegexOptions.IgnoreCase,
                NewFormatting = new Formatting() { FontColor = keywordHighlight }
            };
            newRfc.ReplaceText(replaceTextOptions);
        }
    }
}
