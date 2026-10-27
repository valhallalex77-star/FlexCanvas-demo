using FlexCanvas.Domain.Poc;

namespace FlexCanvas.Application.Poc;

/// <summary>Synchronous native port used only on the prototype's serialized host thread.</summary>
public interface IProbeWindowApi
{
    /// <summary>Captures and validates an unowned, normal, controlled probe.</summary>
    ProbeResult Capture(nint target, nint host, out ProbeSnapshot? snapshot);
    /// <summary>Checks both handle validity and the expected owning process.</summary>
    bool IsAlive(nint hwnd, uint processId);
    /// <summary>Changes style bits and captures native errors immediately.</summary>
    ProbeResult SetStyles(nint hwnd, long style, long extendedStyle);
    /// <summary>Reparents a window without treating a successful zero return as failure.</summary>
    ProbeResult SetParent(nint hwnd, nint parent);
    /// <summary>Moves/resizes in the window's current parent coordinate system.</summary>
    ProbeResult SetBounds(nint hwnd, ProbeBounds bounds);
    /// <summary>Reads the host client area in physical pixels.</summary>
    ProbeResult GetClientBounds(nint hwnd, out ProbeBounds bounds);
}
