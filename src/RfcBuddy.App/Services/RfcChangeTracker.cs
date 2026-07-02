using RfcBuddy.App.Core;
using RfcBuddy.App.Objects;

namespace RfcBuddy.App.Services;

public interface IRfcChangeTracker
{
    RfcChangeStatus GetStatus(Rfc current, IReadOnlyList<PreviousRfc> baseline);
}

public class RfcChangeTracker : IRfcChangeTracker
{
    public RfcChangeStatus GetStatus(Rfc current, IReadOnlyList<PreviousRfc> baseline)
    {
        PreviousRfc? previousRfc = baseline.FirstOrDefault(x => string.Equals(x.RfcNumber, current.RfcNumber, StringComparison.OrdinalIgnoreCase));
        if (previousRfc is null)
        {
            return RfcChangeStatus.New;
        }

        if (current.StartDate == previousRfc.StartDate
            && current.EndDate == previousRfc.EndDate
            && Cryptography.VerifySha256Hash(current.AssetTags, previousRfc.AssetTagsHash)
            && Cryptography.VerifySha256Hash(current.Description, previousRfc.DescriptionHash)
            && Cryptography.VerifySha256Hash(current.RiskAssessment, previousRfc.RiskAssessmentHash))
        {
            return RfcChangeStatus.Unchanged;
        }

        return RfcChangeStatus.Changed;
    }
}
