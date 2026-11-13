using CopperScreen;

namespace CopperScreen.Tests;

// Read the capability from the referenced product assembly, so discovery follows
// the actual build without duplicating its compilation symbols in this project.
public class CopperStartFactAttribute : FactAttribute
{
    internal const string DisabledReason = "CopperStart coverage unavailable in this build. Run with -p:CopperScreenEnableCopperStart=true and a staged qualified ROM/HDF bundle.";

    public CopperStartFactAttribute()
    {
        if (!CopperScreenBootMethods.CopperStartEnabled) Skip = DisabledReason;
    }
}

public sealed class CopperStartTheoryAttribute : TheoryAttribute
{
    public CopperStartTheoryAttribute()
    {
        if (!CopperScreenBootMethods.CopperStartEnabled) Skip = CopperStartFactAttribute.DisabledReason;
    }
}
