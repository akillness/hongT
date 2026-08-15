using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CinderCourt.Sim;
using CinderCourt.View;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class ThreatCuePresentationTests
    {
        readonly List<GameObject> _roots = new List<GameObject>();
        readonly HashSet<GameObject> _preexisting = new HashSet<GameObject>();

        bool _hadReducedMotionPref;
        int _reducedMotionPrefValue;

        [SetUp]
        public void SetUp()
        {
            _hadReducedMotionPref = PlayerPrefs.HasKey("al:reduced-motion");
            _reducedMotionPrefValue = PlayerPrefs.GetInt("al:reduced-motion");
            ViewPrefs.ReducedMotion = false;

            _preexisting.Clear();
            foreach (var existing in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include))
                _preexisting.Add(existing);
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _roots.Count; i++)
                if (_roots[i] != null) Object.DestroyImmediate(_roots[i]);
            _roots.Clear();

            foreach (var live in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include))
                if (live != null && live.transform.parent == null
                    && !_preexisting.Contains(live))
                    Object.DestroyImmediate(live);
            _preexisting.Clear();

            ViewPrefs.ReducedMotion = _reducedMotionPrefValue == 1;
            if (_hadReducedMotionPref)
                PlayerPrefs.SetInt("al:reduced-motion", _reducedMotionPrefValue);
            else
                PlayerPrefs.DeleteKey("al:reduced-motion");
            PlayerPrefs.Save();
        }

        [Test]
        public void ActiveAttackThreats_AppearOnTheSameSync_AndSuppressNonAttackers()
        {
            var director = NewDirector();
            var player = Player();
            var enemies = new[]
            {
                Enemy(0, 1, 650f, ActorAction.Attack, 0.04f),
                Enemy(1, 2, 720f, ActorAction.Move, 0.20f),
            };

            director.SyncActiveAttackThreats(player, enemies);

            Assert.That(EnabledCueCount(director), Is.EqualTo(1),
                "the committed attacker must get a cue on the same view sync, while a moving enemy stays quiet");

            enemies[0] = Enemy(0, 1, 650f, ActorAction.Idle, 0f);
            director.SyncActiveAttackThreats(player, enemies);

            Assert.That(EnabledCueCount(director), Is.EqualTo(0),
                "leaving Attack must suppress the cue immediately so stale ownership never survives");
        }

        [Test]
        public void ActiveAttackThreats_CapAtThree_AndSelectByAgeThenDistanceThenIndex()
        {
            var director = NewDirector();
            var player = Player();
            var enemies = new[]
            {
                Enemy(0, 1, 620f, ActorAction.Attack, 0.10f),
                Enemy(1, 1, 760f, ActorAction.Attack, 0.16f),
                Enemy(2, 1, 700f, ActorAction.Attack, 0.16f),
                Enemy(3, 1, 560f, ActorAction.Attack, 0.22f),
                Enemy(4, 1, 640f, ActorAction.Attack, 0.05f),
            };

            director.SyncActiveAttackThreats(player, enemies);

            Assert.That(EnabledCueCount(director), Is.EqualTo(3),
                "court readability allows one primary plus two room threats, never all attackers");
            AssertCuePointsAt(director, 0, player, enemies[3],
                "largest ActionTime entered Attack earliest and owns primary salience");
            AssertCuePointsAt(director, 1, player, enemies[2],
                "same ActionTime breaks by iso distance before list order");
            AssertCuePointsAt(director, 2, player, enemies[1],
                "after age and distance, the remaining older attacker fills the final slot");
        }

        [Test]
        public void ActiveAttackThreats_ReducedMotionKeepsAStaticReadableShape()
        {
            var director = NewDirector();
            var player = Player();
            var enemies = new[] { Enemy(0, -1, 690f, ActorAction.Attack, 0.11f) };

            ViewPrefs.ReducedMotion = true;
            director.SyncActiveAttackThreats(player, enemies);
            var cue = Cue(director, 0);
            var a = cue.GetPosition(0);
            var b = cue.GetPosition(1);
            var c = cue.GetPosition(2);
            var startWidth = cue.startWidth;
            var endWidth = cue.endWidth;
            var alpha = cue.sharedMaterial.color.a;

            director.SyncActiveAttackThreats(player, enemies);

            Assert.That(cue.enabled, Is.True, "reduced motion must preserve the geometric attack cue");
            Assert.That(cue.GetPosition(0), Is.EqualTo(a));
            Assert.That(cue.GetPosition(1), Is.EqualTo(b));
            Assert.That(cue.GetPosition(2), Is.EqualTo(c));
            Assert.That(cue.startWidth, Is.EqualTo(startWidth));
            Assert.That(cue.endWidth, Is.EqualTo(endWidth));
            Assert.That(cue.sharedMaterial.color.a, Is.EqualTo(alpha));
        }

        VfxDirector NewDirector()
        {
            var root = new GameObject("ThreatCuePresentationTestRoot");
            _roots.Add(root);
            var director = root.AddComponent<VfxDirector>();
            typeof(VfxDirector).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(director, null);
            return director;
        }

        static PlayerState Player() => new PlayerState
        {
            X = 600f,
            Y = 604f,
            Facing = 1,
            Health = SimConfig.PlayerMaxHealth,
            Action = ActorAction.Idle,
        };

        static EnemyState Enemy(
            int index, int facing, float x, ActorAction action, float actionTime) => new EnemyState
        {
            Id = index + 1,
            Visual = EnemyVisual.EmberCohort,
            X = x,
            Y = 604f,
            Facing = facing,
            Health = 100f,
            MaxHealth = 100f,
            Action = action,
            ActionTime = actionTime,
            Scale = 1f,
        };

        static int EnabledCueCount(VfxDirector director)
        {
            var count = 0;
            for (var i = 0; i < 3; i++)
                if (Cue(director, i).enabled) count += 1;
            return count;
        }

        static LineRenderer Cue(VfxDirector director, int index)
        {
            var cue = director.transform.Find("ActiveThreatCue" + index);
            Assert.That(cue, Is.Not.Null, "the active-threat cue pool must be preallocated under VfxDirector");
            var line = cue.GetComponent<LineRenderer>();
            Assert.That(line, Is.Not.Null);
            return line;
        }

        static void AssertCuePointsAt(
            VfxDirector director, int slot, in PlayerState player, in EnemyState enemy, string reason)
        {
            var line = Cue(director, slot);
            Assert.That(line.enabled, Is.True, reason);
            Assert.That(Vector3.Distance(line.GetPosition(1), ExpectedApex(slot, player, enemy)),
                Is.LessThan(0.001f), reason);
        }

        static Vector3 ExpectedApex(int slot, in PlayerState player, in EnemyState enemy)
        {
            var attacker = ViewWorld.ToWorld(enemy.X, enemy.Y, 0.13f);
            var target = ViewWorld.ToWorld(player.X, player.Y, 0.13f);
            var direction = target - attacker;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = new Vector3(enemy.Facing >= 0 ? 1f : -1f, 0f, 0f);
            else
                direction.Normalize();
            return attacker + direction * (slot == 0 ? 0.82f : 0.62f);
        }
    }
}
