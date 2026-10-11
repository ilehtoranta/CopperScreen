// Copyright (C) 2026 Ilkka Lehtoranta. SPDX-License-Identifier: MIT
namespace CopperMod.Amiga.Lightweight;

// Immutable fitted traits specialize shared CCK code. Live register controls
// remain device state and take effect at their existing input/output phases.
internal interface ILightweightExecutionPath
{
    static abstract bool EnhancedDisplay { get; }
    static abstract bool Aga { get; }
}

internal readonly struct OcsExecutionPath : ILightweightExecutionPath
{
    public static bool EnhancedDisplay => false;
    public static bool Aga => false;
}

internal readonly struct EcsExecutionPath : ILightweightExecutionPath
{
    public static bool EnhancedDisplay => true;
    public static bool Aga => false;
}

internal readonly struct AgaExecutionPath : ILightweightExecutionPath
{
    public static bool EnhancedDisplay => true;
    public static bool Aga => true;
}
