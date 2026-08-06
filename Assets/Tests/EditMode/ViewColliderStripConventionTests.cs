// Convention gate: nothing in the View assembly strips a Collider with an
// unguarded Destroy.
//
// WHAT SILENTLY BREAKS WITHOUT THIS GATE. Object.Destroy is a NO-OP outside
// play mode. Unity logs "Destroy may not be called from edit mode" and removes
// nothing, so the collider LIVES ON. Nothing throws, nothing fails to compile,
// no build breaks, and the game looks identical in play mode — the only symptom
// is an editor console line a human has to notice. That is not hypothetical:
// this rule was established in ActorView, and it had already drifted out of
// THREE files (VfxDirector's ~20 primitive sites, ActorView.FlashCastGlow, and
// LobbyStaging.Compose's capsule fallback) before anyone spotted the console
// noise. Decoration in this project must never own physics — the sim owns all
// collision — so a surviving collider is a real defect, just a quiet one.
//
// WHY A SOURCE RULE, AND WHAT IT CANNOT DO. The runtime counterpart lives in
// SkillShapeVocabularyTests.EveryPrimitiveTheDirectorBuilds_LosesItsCollider,
// which brings up a VfxDirector and asserts no live Collider survives. That
// proves BEHAVIOUR, but only for the one class it can construct. The other two
// call sites sit behind setup a fixture cannot cheaply reach — FlashCastGlow
// early-returns unless the animator is humanoid and the RightHand bone
// resolves; LobbyStaging needs its compose path — and no runtime fixture can
// cover a file nobody has written yet. This gate is the complement: it forbids
// the unguarded SPELLING across the whole assembly, including future files. It
// asserts how code is written, which is what a lint rule is for, and it is
// deliberately NOT a substitute for the behavioural test above. Neither alone
// is sufficient: this one cannot prove a collider is actually gone, and that
// one cannot see a file it never instantiates.
//
// SCOPE IS ONE ASSEMBLY, ON PURPOSE. The rule is asserted over
// Assets/Scripts/View only, because that is where the convention was
// established and where every current call site lives. Extending it to another
// directory needs its own check that the directory really is collider-stripping
// territory — a blanket ban across the repo would flag legitimate physics code
// that genuinely wants a collider removed at runtime.
//
// WHAT IS NOT BANNED. Unguarded Destroy on non-collider objects is fine and
// common here: VfxDirector destroys hazard roots, pickup views and flying
// pickups; ActorView destroys equip props, ghosts, baked meshes and cloned
// materials; LobbyStaging destroys the boss and companion instances. Ten such
// calls exist today and none is examined — every check below is anchored on a
// collider ACQUISITION, so code that never asks for a collider is never judged.
// Unguarded DestroyImmediate is likewise fine: it removes the collider in both
// modes, which is the whole contract. Only Destroy needs the play-mode fork,
// because only Destroy is a no-op outside it.
//
// This scans source text the way FontCoverageTests harvests Korean glyphs out of
// the same directory, for the same reason: the repo has no separate C# lint
// pass, so the EditMode suite IS where source conventions are enforced.
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class ViewColliderStripConventionTests
    {
        /// <summary>
        /// The acquisition every check hangs off. Code that never asks for a
        /// collider is never examined, which is what keeps the assembly's ten
        /// legitimate unguarded Destroy calls out of scope.
        ///
        /// Deliberately NOT the literal "GetComponent&lt;Collider&gt;".
        /// CreatePrimitive attaches CONCRETE types — BoxCollider (Cube),
        /// SphereCollider (Sphere), CapsuleCollider (Capsule), MeshCollider
        /// (Quad/Plane) — so `Destroy(go.GetComponent&lt;SphereCollider&gt;())`
        /// is the spelling a future author most naturally reaches for. A literal
        /// anchor cannot see it and the gate would sit green on exactly the bug
        /// it exists to stop. `\w*Collider\w*` covers the concrete types and
        /// Collider2D; InChildren/InParent and the typeof() overload are covered
        /// by the alternation.
        ///
        /// NOTE for anyone tightening this: there is no `\b` before
        /// GetComponent, and that is load-bearing. TryGetComponent is caught
        /// only because "TryGetComponent" CONTAINS "GetComponent" — adding a
        /// word boundary would silently stop matching it, since Try and
        /// GetComponent are both word characters with no boundary between them.
        /// </summary>
        static readonly Regex ColliderAcquisition = new Regex(
            @"GetComponent(?:InChildren|InParent)?\s*<\s*\w*Collider\w*\s*>" +
            @"|GetComponent(?:InChildren|InParent)?\s*\(\s*typeof\s*\(\s*\w*Collider\w*\s*\)");

        /// <summary>The play-mode fork that makes a Destroy correct:
        /// <c>if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);</c>
        /// Matched on the qualified form — a bare "isPlaying" would also hit
        /// AudioSource.isPlaying, which AudioDirector uses for BGM state.</summary>
        const string PlayModeGuard = "Application.isPlaying";

        /// <summary>The edit-mode-safe call. Present in the correct fork's else
        /// branch, and compliant on its own.</summary>
        const string ImmediateCall = "DestroyImmediate(";

        /// <summary>Lines BELOW an acquisition still considered part of the same
        /// idiom. The widest real spelling is LobbyStaging.Compose, where a
        /// null-check block puts three lines between acquisition and Destroy.</summary>
        const int GuardWindowLines = 6;

        /// <summary>
        /// Lines ABOVE an acquisition that can carry its guard. Non-zero because
        /// a forward-only window is not merely narrow, it is DIRECTIONALLY blind:
        /// a guard one line above is invisible at any window width, and it flags
        /// two correct shapes —
        ///
        ///     if (Application.isPlaying) { var c = go.GetComponent&lt;Collider&gt;(); Destroy(c); }
        ///
        /// and the early-return form that puts `if (!Application.isPlaying)`
        /// above the Destroy. Both are legitimate; both were reported as
        /// violations before this existed.
        ///
        /// Six, matching the forward window, because four was one line short of
        /// the early-return shape when it acquires the collider on both branches.
        /// Looking back used to risk masking a real strip that happened to sit
        /// below an unrelated fork — GameDirector and GameView both fork on
        /// non-collider objects — but <see cref="OpensAScope"/> now discards
        /// those, so the window can cover the real shapes without buying a miss.
        /// </summary>
        const int GuardLookbackLines = 6;

        /// <summary>Floor on files scanned. Scripts/View holds 24 .cs files, so a
        /// harvest finding almost nothing means the path or the glob broke rather
        /// than the convention being satisfied.</summary>
        const int MinimumViewFiles = 10;

        /// <summary>A Destroy CALL, not the bare word: the open paren keeps prose
        /// like "Destroy is a no-op outside play mode" — which appears in the very
        /// comments explaining this convention — from reading as code. Cannot
        /// match DestroyImmediate, since the paren cannot follow "Destroy"
        /// there.</summary>
        static readonly Regex DestroyCall = new Regex(@"\bDestroy\s*\(");

        /// <summary>
        /// Whether a play-mode guard actually protects the strip on
        /// <paramref name="stripLine"/>. Bounded by the STRIP, not by a blind
        /// window around the acquisition: a guard BELOW the strip cannot protect
        /// it, and counting one that does is how a bare Destroy followed by an
        /// unrelated `if (Application.isPlaying)` used to read as compliant.
        /// </summary>
        static bool GuardProtects(string[] lines, int acquisitionLine, int stripLine)
        {
            // On the strip itself: the one-liner fork both helpers use.
            if (lines[stripLine].Contains(PlayModeGuard)) return true;

            var from = Mathf.Max(acquisitionLine - GuardLookbackLines, 0);
            for (var j = from; j < stripLine; j++)
                if (lines[j].Contains(PlayModeGuard) && OpensAScope(lines[j]))
                    return true;
            return false;
        }

        /// <summary>
        /// Whether a guard line ABOVE a strip can reach it — decided on brace
        /// BALANCE, not brace presence. A guard only governs what follows if it
        /// leaves a scope OPEN.
        ///
        ///     if (Application.isPlaying)                      opens (bare condition)
        ///     if (Application.isPlaying) {                    opens (1 &gt; 0)
        ///     if (Application.isPlaying) { Destroy(x); }      does NOT (1 == 1)
        ///     if (Application.isPlaying) Destroy(_terrain);   does NOT (own statement)
        ///
        /// The last two govern their own statement and nothing after it, so they
        /// must not lend their guard to a later, unrelated strip. Testing for a
        /// mere "{" got the braced one-liner wrong and masked a real bare strip
        /// below it — the same false-negative class this method exists to close,
        /// one spelling over. Testing brace balance ALONE gets the opposite case
        /// wrong: `if (Application.isPlaying)` with the block on the next line
        /// carries no brace at all, so it would go unrecognised and correct code
        /// would be flagged. Both clauses are load-bearing; neither alone is right.
        ///
        /// PRECONDITION, and the reason the second clause is safe: the only
        /// caller filters on <see cref="PlayModeGuard"/> first, so every line
        /// reaching here already contains the guard. A bare "}" — the one shape
        /// the second clause would misread as scope-opening — can never arrive.
        /// Calling this from an unfiltered site reintroduces that hole.
        /// </summary>
        static bool OpensAScope(string guardLine)
        {
            var depth = 0;
            foreach (var c in guardLine)
            {
                if (c == '{') depth++;
                else if (c == '}') depth--;
            }
            if (depth > 0) return true;
            return !guardLine.Contains(";") && !guardLine.Contains("{");
        }

        [Test]
        public void NoViewSource_StripsAColliderWithAnUnguardedDestroy()
        {
            var viewDir = Path.Combine(Application.dataPath, "Scripts/View");
            Assert.That(Directory.Exists(viewDir), Is.True,
                $"{viewDir} does not exist — the View assembly moved and this gate is " +
                "scanning nothing, so it would pass forever without reading a line of code");

            // AllDirectories rather than the flat default: the whole value of a
            // source rule is covering files nobody has written yet, and a View
            // subdirectory added later must not silently escape the scan.
            var files = Directory.GetFiles(viewDir, "*.cs", SearchOption.AllDirectories);

            var violations = new List<string>();
            var acquisitions = 0;
            var compliant = 0;

            foreach (var path in files)
            {
                var lines = File.ReadAllLines(path);
                var fileName = Path.GetFileName(path);

                for (var i = 0; i < lines.Length; i++)
                {
                    if (!ColliderAcquisition.IsMatch(lines[i])) continue;
                    acquisitions++;

                    var to = Mathf.Min(i + 1 + GuardWindowLines, lines.Length);

                    // SHAPE 1 — inline: strip and acquisition are one statement,
                    // e.g. Destroy(x.GetComponent<Collider>()). The spelling all
                    // three drifted files actually used.
                    if (DestroyCall.IsMatch(lines[i]))
                    {
                        if (GuardProtects(lines, i, i)) compliant++;
                        else violations.Add($"{fileName}:{i + 1}: {lines[i].Trim()}");
                        continue;
                    }

                    // SHAPE 2 — via a local: acquire, then strip below. Both
                    // correct spellings in the assembly look like this.
                    var strippedAt = -1;
                    var immediateAt = -1;
                    for (var j = i + 1; j < to; j++)
                    {
                        if (strippedAt < 0 && DestroyCall.IsMatch(lines[j])) strippedAt = j;
                        if (immediateAt < 0 && lines[j].Contains(ImmediateCall)) immediateAt = j;
                    }

                    // A bare Destroy is the only defect. DestroyImmediate alone
                    // is COMPLIANT and must never be flagged — it removes the
                    // collider in both modes. Counting it as compliant is what
                    // keeps the floor below from failing when an author picks a
                    // correct idiom this gate did not anticipate; a gate that
                    // fails where there is no defect gets deleted, and then
                    // nothing guards the convention at all.
                    if (strippedAt < 0)
                    {
                        if (immediateAt >= 0 || lines[i].Contains(ImmediateCall)) compliant++;
                        continue;   // acquired, then stripped safely or not at all
                    }
                    if (GuardProtects(lines, i, strippedAt)) compliant++;
                    else violations.Add($"{fileName}:{strippedAt + 1}: {lines[strippedAt].Trim()}");
                }
            }

            // ORDER MATTERS. The violation list is asserted FIRST because a real
            // unguarded strip also drags the compliant count down, and a floor
            // tripping first would tell the author "the idiom was replaced" when
            // the truth is "you left a bare Destroy on line N".
            Assert.That(violations, Is.Empty,
                $"{violations.Count} unguarded Collider strip(s) in Assets/Scripts/View:\n  " +
                string.Join("\n  ", violations) +
                $"\n\nObject.Destroy is a NO-OP outside play mode: the collider SURVIVES, Unity " +
                "logs \"Destroy may not be called from edit mode\", and nothing else fails — so " +
                "decoration silently keeps physics the sim never asked for. Fork on play mode " +
                "instead:\n" +
                "    var collider = go.GetComponent<Collider>();\n" +
                "    if (collider == null) return;\n" +
                "    if (Application.isPlaying) Destroy(collider);\n" +
                "    else DestroyImmediate(collider);\n" +
                "ActorView and VfxDirector wrap exactly that in a private static " +
                "RemovePrimitiveCollider helper; call one of those, mirror the helper, or inline " +
                $"the fork within {GuardWindowLines} lines below (or {GuardLookbackLines} above) " +
                "the acquisition. An unguarded DestroyImmediate is also fine.");

            // Non-vacuity. A scan that reads no files, matches no acquisitions,
            // or recognises no compliant strip would report a clean assembly
            // while judging nothing — the inert-guard trap this suite has already
            // been bitten by once.
            Assert.That(files.Length, Is.GreaterThanOrEqualTo(MinimumViewFiles),
                $"only {files.Length} .cs files found under {viewDir}; at least " +
                $"{MinimumViewFiles} are expected, so the scan is not reading the View " +
                "assembly and every assertion above is vacuous");

            // Floors are 1, NOT today's count of 3. Pinning them at 3 would make
            // the gate fail on a desirable cleanup: ActorView's and VfxDirector's
            // RemovePrimitiveCollider helpers are duplicates, and consolidating
            // them into one shared helper drops acquisitions to 2 — red on
            // correct code, with no defect behind it. What these floors must
            // prove is only that the detector still SEES something and can still
            // tell compliant from not; the violations list above is the actual
            // signal, and it needs no floor to work.
            Assert.That(acquisitions, Is.GreaterThanOrEqualTo(1),
                $"no collider acquisition matched across {files.Length} files. The anchor no " +
                "longer matches how this assembly asks for a Collider (concrete types, " +
                "InChildren/InParent and the typeof overload are all covered, so this means the " +
                "spelling changed again), and nothing is being judged");

            Assert.That(compliant, Is.GreaterThanOrEqualTo(1),
                $"{acquisitions} collider acquisition(s) matched and none was recognised as a " +
                $"compliant strip. Both the '{PlayModeGuard}' fork and a bare " +
                $"'{ImmediateCall}' should count, so this gate can no longer tell a safe strip " +
                "from an unguarded one and its clean result means nothing");
        }
    }
}
