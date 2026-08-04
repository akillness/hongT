// EditMode layout tests drive HudView's injected-geometry seam
// (ApplyLayout / ForceTouchControlsForTest / CurrentTier) directly:
// Screen.* is read-only and reports degenerate sizes in batchmode.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CinderCourt.Tests.EditMode")]
