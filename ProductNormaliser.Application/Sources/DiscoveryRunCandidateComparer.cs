using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

internal static class DiscoveryRunCandidateComparer
{
    public static bool ArePotentialDuplicates(DiscoveryRunCandidate left, DiscoveryRunCandidate right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (string.Equals(left.CandidateKey, right.CandidateKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(DiscoveryRunCandidateIdentity.NormalizeHost(left.Host), DiscoveryRunCandidateIdentity.NormalizeHost(right.Host), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(DiscoveryRunCandidateIdentity.NormalizeBaseUrl(left.BaseUrl), DiscoveryRunCandidateIdentity.NormalizeBaseUrl(right.BaseUrl), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(DiscoveryRunCandidateIdentity.NormalizeName(left.DisplayName), DiscoveryRunCandidateIdentity.NormalizeName(right.DisplayName), StringComparison.OrdinalIgnoreCase)
            && DiscoveryRunCandidateIdentity.ShareAnyMarket(left.AllowedMarkets, right.AllowedMarkets);
    }
}