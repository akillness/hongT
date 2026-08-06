// EditMode tests pin the sim's pure decision functions directly rather than
// sampling them through a run: ResolveFinisherVariant and ComboVariant have
// small, total input spaces, so they are asserted exhaustively. Exposing them
// publicly would widen the frozen Sim contract for a test's benefit; the
// friend grant keeps the shipped surface unchanged. Mirrors the same grant in
// Assets/Scripts/View/AssemblyInfo.cs.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CinderCourt.Tests.EditMode")]
