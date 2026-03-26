using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

internal static class DiscoveryRunStateMachine
{
    public static bool CanPause(string status)
    {
        return string.Equals(status, DiscoveryRunStatuses.Queued, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DiscoveryRunStatuses.Running, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanResume(string status)
    {
        return string.Equals(status, DiscoveryRunStatuses.Paused, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanStopImmediately(string status)
    {
        return string.Equals(status, DiscoveryRunStatuses.Queued, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DiscoveryRunStatuses.Paused, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanRequestStop(string status)
    {
        return string.Equals(status, DiscoveryRunStatuses.Running, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTerminal(string status)
    {
        return string.Equals(status, DiscoveryRunStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DiscoveryRunStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DiscoveryRunStatuses.Failed, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanAcceptCandidate(string state)
    {
        return string.Equals(state, DiscoveryRunCandidateStates.Suggested, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanDismissCandidate(string state)
    {
        return string.Equals(state, DiscoveryRunCandidateStates.Suggested, StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, DiscoveryRunCandidateStates.Failed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, DiscoveryRunCandidateStates.Archived, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanRestoreCandidate(string state)
    {
        return string.Equals(state, DiscoveryRunCandidateStates.Dismissed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, DiscoveryRunCandidateStates.Archived, StringComparison.OrdinalIgnoreCase);
    }
}