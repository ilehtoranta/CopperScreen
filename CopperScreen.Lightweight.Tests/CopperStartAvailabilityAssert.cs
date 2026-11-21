using CopperScreen;

namespace CopperScreen.Tests;

internal static class CopperStartAvailabilityAssert
{
    internal static void ForProfile(CopperScreenSettingsDraft draft, string directory, string? enabledError = null)
    {
        var actual = CopperScreenAvailability.GetUnavailableReason(draft, directory);
        var expected = CopperScreenBootMethods.CopperStartEnabled ? enabledError : "CopperStart is not enabled in this build";
        if (expected is null) Assert.Null(actual);
        else Assert.Contains(expected, actual);
    }
}
