using RfcBuddy.App.Core;
using RfcBuddy.App.Objects;
using System.Text.RegularExpressions;
using Xceed.Document.NET;
using Xceed.Drawing;
using Xceed.Words.NET;

namespace RfcBuddy.App.Services;

/// <summary>
/// Service to create a Word document from the given RFCs
/// </summary>
public interface IWordService
{
    void CreateWordFile(ref Stream wordFile, List<Rfc> ministryRfcs, List<Rfc> generalRfcs, List<Rfc> otherRfcs, List<PreviousRfc> previousRfcs);
}

public class WordService : IWordService
{
    // Use Xceed.Document.NET.Color instead of System.Drawing.Color
    private readonly Color ministryHighlight = Color.Red;
    private readonly Color generalHighlight = Color.Purple;
    private readonly Color sundayHighlight = Color.Green;
    private readonly Color changeHighlight = Color.Blue;

    private const string wordDateFormat = "ddd yyyy-MM-dd HH:mm";

    public void CreateWordFile(ref Stream wordFile, List<Rfc> ministryRfcs, List<Rfc> generalRfcs, List<Rfc> otherRfcs, List<PreviousRfc> previousRfcs)
    {
        using DocX document = DocX.Create(wordFile);

        // Header and legend
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

        // Ministry RFCs
        Paragraph ministry = document.InsertParagraph("Ministry: " + ministryRfcs.Count + " RFCs");
        ministry.StyleId = "Heading1";
        ministry.Color(ministryHighlight);
        AddRfcSection(document, ministryRfcs, previousRfcs, ministryHighlight);

        // General RFCs
        Paragraph general = document.InsertParagraph("General: " + generalRfcs.Count + " RFCs");
        general.StyleId = "Heading1";
        general.Color(generalHighlight);
        AddRfcSection(document, generalRfcs, previousRfcs, generalHighlight);

        // Other RFCs
        Paragraph other = document.InsertParagraph("Other / unclassified: " + otherRfcs.Count + " RFCs");
        other.StyleId = "Heading1";
        AddRfcSection(document, otherRfcs, previousRfcs, Color.Black);

        document.Save();
    }

    private void AddRfcSection(DocX document, List<Rfc> rfcs, List<PreviousRfc> previousRfcs, Color keywordHighlight)
    {
        DateTime now = DateTime.Now;
        List<string> unchangedRfcs = new List<string>();

        // In Progress
        Paragraph pProgress = document.InsertParagraph("In Progress");
        pProgress.StyleId = "Heading2";
        int pProgressCount = 0;
        foreach (Rfc currentRfc in rfcs.Where(x => x.StartDate <= now && x.EndDate >= now))
        {
            AddRfc(document, currentRfc, null, keywordHighlight);
            document.InsertParagraph();
            document.InsertParagraph();
            pProgressCount++;
        }
        if (pProgressCount == 0) document.InsertParagraph("No RFCs found.");

        // New or Changed
        Paragraph pNew = document.InsertParagraph("New or Changed");
        pNew.StyleId = "Heading2";
        int pNewCount = 0;
        foreach (Rfc currentRfc in rfcs.Where(x => x.StartDate > now || x.EndDate < now))
        {
            PreviousRfc? previousRfc = previousRfcs.Find(x => x.RfcNumber == currentRfc.RfcNumber);
            if (previousRfc != null
                && currentRfc.StartDate == previousRfc.StartDate
                && currentRfc.EndDate == previousRfc.EndDate
                && Cryptography.VerifySha256Hash(currentRfc.AssetTags, previousRfc.AssetTagsHash)
                && Cryptography.VerifySha256Hash(currentRfc.Description, previousRfc.DescriptionHash)
                && Cryptography.VerifySha256Hash(currentRfc.RiskAssessment, previousRfc.RiskAssessmentHash))
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
        if (pNewCount == 0) document.InsertParagraph("No RFCs found.");

        // Previously Reviewed
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
        if (pPreviousCount == 0) document.InsertParagraph("No RFCs found.");
    }

    private void AddRfc(DocX document, Rfc rfc, PreviousRfc? previousRfc, Color keywordHighlight)
    {
        Paragraph newRfc = document.InsertParagraph();
        newRfc.Append("RFC: ").Bold().Append(rfc.RfcNumber.ToString());
        newRfc.Append("\tStart: ").Bold();
        if (previousRfc != null && rfc.StartDate != previousRfc.StartDate)
            newRfc.Append(rfc.StartDate.ToString(wordDateFormat)).Color(changeHighlight);
        else if (rfc.StartDate.DayOfWeek == DayOfWeek.Sunday && rfc.StartDate.ToString("HH:mm:ss") == "06:00:00")
            newRfc.Append(rfc.StartDate.ToString(wordDateFormat)).Color(sundayHighlight);
        else
            newRfc.Append(rfc.StartDate.ToString(wordDateFormat));

        newRfc.Append("\t\tEnd: ").Bold();
        if (previousRfc != null && rfc.EndDate != previousRfc.EndDate)
            newRfc.Append(rfc.EndDate.ToString(wordDateFormat)).Color(changeHighlight);
        else if (rfc.EndDate.DayOfWeek == DayOfWeek.Sunday && rfc.EndDate.ToString("HH:mm:ss") == "09:00:00")
            newRfc.Append(rfc.EndDate.ToString(wordDateFormat)).Color(sundayHighlight);
        else
            newRfc.Append(rfc.EndDate.ToString(wordDateFormat));

        newRfc.AppendLine();
        newRfc.Append("Status: ").Bold().Append(rfc.ApprovalStatus);
        newRfc.Append("\tPlatform: ").Bold().Append(rfc.Platform);
        newRfc.Append("\tAsset Tags: ").Bold();
        if (previousRfc == null || Cryptography.VerifySha256Hash(rfc.AssetTags, previousRfc.AssetTagsHash))
            newRfc.Append(rfc.AssetTags);
        else
            newRfc.Append(rfc.AssetTags).Color(changeHighlight);

        newRfc.AppendLine();
        newRfc.Append("Description: ").Bold();
        if (previousRfc == null || Cryptography.VerifySha256Hash(rfc.Description, previousRfc.DescriptionHash))
            newRfc.Append(rfc.Description);
        else
            newRfc.Append(rfc.Description).Color(changeHighlight);

        newRfc.AppendLine();
        newRfc.Append("Risk Assessment: ").Bold();
        if (previousRfc == null || Cryptography.VerifySha256Hash(rfc.RiskAssessment, previousRfc.RiskAssessmentHash))
            newRfc.Append(rfc.RiskAssessment);
        else
            newRfc.Append(rfc.RiskAssessment).Color(changeHighlight);

        // Highlight keywords
        foreach (string keyword in rfc.Keywords)
        {
            newRfc.ReplaceText(new StringReplaceTextOptions
            {
                SearchValue = @"\b" + keyword + @"\b",
                NewValue = keyword,
                TrackChanges = false,
                EscapeRegEx = false,
                RegExOptions = RegexOptions.IgnoreCase,
                NewFormatting = new Formatting() { FontColor = keywordHighlight }
            });
        }
    }
}
