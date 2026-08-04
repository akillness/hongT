// Deterministic fixed-step port of the original Cinder Court run loop.
// Numeric truth: docs/SIM_SPEC.md. Contract: Assets/Scripts/Sim/SimTypes.cs (FROZEN).
// No UnityEngine, no RNG, no LINQ, no per-tick heap allocation.
using System;
using System.Collections.Generic;

namespace CinderCourt.Sim
{
    /// <summary>
    /// Pure C# simulation. One <see cref="Tick"/> advances exactly 1/60 s and runs the
    /// original fixedUpdate order: player -> enemies -> skills -> pickups -> wave.
    /// </summary>
    public sealed class CinderSim : ICinderSim
    {
        // --- spec constants that SimConfig does not expose (docs/SIM_SPEC.md) ---
        private const float EnemyHealthPerWave = 9f;        // 58 + min(92, (wave-1)*9)
        private const float EnemyHealthWaveCap = 92f;
        private const float EnemyCooldownPerWave = 0.025f;  // 1.22 + min(0.38, wave*0.025)
        private const float EnemyCooldownWaveCap = 0.38f;
        private const float EnemySpeedBase = 78f;           // min(128, 78 + wave*3.2 + (id%3)*2.5)
        private const float EnemySpeedPerWave = 3.2f;
        private const float EnemySpeedIdStep = 2.5f;
        private const float EnemySpeedCap = 128f;
        private const float ContactDamageBase = 7f;         // min(18, 7 + floor(wave*0.8))
        private const float ContactDamagePerWave = 0.8f;
        private const float ContactDamageCap = 18f;
        private const float FirstAttackDelayStep = 0.18f;   // (id%3)*0.18
        private const float EnemyChaseSlack = 5f;           // chase while distance > range-5
        private const float EnemyFacingDeadzone = 4f;       // facing flips when |dx| > 4
        private const int WaveSpawnBase = 3;                // min(20, 3 + floor(wave*1.2))
        private const float WaveSpawnPerWave = 1.2f;
        private const float SpawnIntervalBase = 0.62f;      // max(0.28, 0.62 - wave*0.018)
        private const float SpawnIntervalPerWave = 0.018f;
        private const float SpawnIntervalMin = 0.28f;
        private const int KillScorePerWave = 100;
        private const int BossKillScorePerWave = 1000;
        private const float NovaFlashDuration = 0.42f;
        private const int AttackClipFrames = 5;             // 5-frame attack clip @ 12 fps
        private const float AttackClipFps = 12f;
        private const int AttackActiveFirstFrame = 2;       // SimConfig.AttackActiveFrom
        private const int AttackActiveLastFrame = 3;        // SimConfig.AttackActiveTo (exclusive frame 4)
        private const int EnemyContactFrame = 2;            // SimConfig.EnemyContactDelay
        private const float SeparationMinDistanceSq = 0.01f;
        private const float MoveEpsilon = 0.001f;
        private const int VisualRotation = 4;               // (wave + spawnIndex) % 4
        private const int BossVisualPeriod = 10;            // wave%10==5 commander, wave%10==0 monarch
        private const string OverrunReason = "overrun";
        private const string RunningReason = "";

        /// <summary>Enemy record: the published state plus sim-only bookkeeping.</summary>
        private struct Enemy
        {
            public EnemyState State;
            public float AttackCooldown;
            public bool DidDamage;
            public int LastHitAttack;
        }

        private Enemy[] _enemies = new Enemy[SimConfig.EnemyCap];
        private int _enemyCount;
        private PickupState[] _pickups = new PickupState[SimConfig.EnemyCap];
        private int _pickupCount;

        private readonly List<EnemyState> _enemyView = new List<EnemyState>(SimConfig.EnemyCap);
        private readonly List<PickupState> _pickupView = new List<PickupState>(SimConfig.EnemyCap);

        private PlayerState _player;
        private SimMode _mode;
        private SimEvents _events;

        private int _wave;
        private int _waveSeed;
        private int _score;
        private int _kills;
        private int _relics;
        private float _charge;
        private float _novaCooldown;
        private float _wardCooldown;
        private float _novaFlash;
        private float _novaX;
        private float _novaY;
        private int _pendingSpawns;
        private bool _pendingBoss;
        private int _spawnIndexInWave;
        private float _spawnTimer;
        private float _intermission;
        private int _livingEnemies;
        private int _nextEnemyId;
        private int _nextPickupId;
        private string _reason;

        public CinderSim()
        {
            Restart();
        }

        // --- ISimSnapshot ----------------------------------------------------
        public SimMode Mode => _mode;
        public int Wave => _wave;
        public int Score => _score;
        public int Kills => _kills;
        public int Relics => _relics;
        public float Charge => _charge;
        public float NovaCooldown => _novaCooldown;
        public float WardCooldown => _wardCooldown;
        public float NovaFlash => _novaFlash;
        public int PendingSpawns => _pendingSpawns;
        public int LivingEnemies => _livingEnemies;
        public PlayerState Player => _player;
        public IReadOnlyList<EnemyState> Enemies => _enemyView;
        public IReadOnlyList<PickupState> Pickups => _pickupView;
        public SimEvents Events => _events;
        public float NovaX => _novaX;
        public float NovaY => _novaY;

        public RunDigest Digest => new RunDigest
        {
            Score = _score,
            Wave = _wave,
            Kills = _kills,
            Relics = _relics,
            HealthRemaining = _player.Health,
            Reason = _reason,
        };

        // --- Pure wave arithmetic (shared by sim and tests) -------------------

        /// <summary>Spawn queue length for a wave, boss slot included, enemy cap applied.</summary>
        public static int SpawnCountForWave(int wave)
        {
            int queued = WaveSpawnBase + (int)MathF.Floor(wave * WaveSpawnPerWave);
            if (IsBossWave(wave))
            {
                queued += 1;
            }
            return Math.Min(SimConfig.EnemyCap, queued);
        }

        /// <summary>True when the wave opens with one extra boss spawn.</summary>
        public static bool IsBossWave(int wave) => wave % SimConfig.BossEveryWaves == 0;

        /// <summary>Spawn point index for an enemy id: (waveSeed + id*3) % 8.</summary>
        public static int SpawnPointIndexFor(int wave, int enemyId)
        {
            int waveSeed = (wave * 3) % SimConfig.SpawnPoints.Length;
            return (waveSeed + enemyId * 3) % SimConfig.SpawnPoints.Length;
        }

        // --- ICinderSim ------------------------------------------------------

        public void Restart()
        {
            _enemyCount = 0;
            _pickupCount = 0;
            _livingEnemies = 0;
            _score = 0;
            _kills = 0;
            _relics = 0;
            _charge = SimConfig.LanternMax;
            _novaCooldown = 0f;
            _wardCooldown = 0f;
            _novaFlash = 0f;
            _nextEnemyId = 1;
            _nextPickupId = 1;
            _reason = RunningReason;
            _events = SimEvents.None;

            _player = default;
            _player.X = SimConfig.ArenaX;
            _player.Y = SimConfig.ArenaY + SimConfig.PlayerStartYOffset;
            _player.Facing = 1;
            _player.Health = SimConfig.PlayerMaxHealth;
            _player.Action = ActorAction.Idle;
            _player.ActionTime = 0f;
            _player.AttackId = 0;

            _novaX = _player.X;
            _novaY = _player.Y;

            StartWave(1);
            _events = SimEvents.None;
            Publish();
        }

        public void Tick(in SimInput input)
        {
            _events = SimEvents.None;

            // The original restarts from the key handler, i.e. between frames: the
            // restarted state is what the next step sees.
            if (input.RestartQueued)
            {
                Restart();
                return;
            }

            if (_mode != SimMode.Running && _mode != SimMode.WaveClear)
            {
                return;
            }

            const float dt = SimConfig.FixedStep;

            // Skill keys land between frames in the original, so they resolve before
            // the step body: a ward cast is already up for this step's enemy contacts.
            CastSkills(in input);

            UpdatePlayer(dt, in input);
            UpdateEnemies(dt);
            if (_mode != SimMode.GameOver)
            {
                UpdateSkills(dt);
                UpdatePickups(dt);
                UpdateWave(dt);
            }

            Publish();
        }

        // --- Skills ----------------------------------------------------------

        private void CastSkills(in SimInput input)
        {
            if (input.NovaQueued && _novaCooldown <= 0f && _charge >= SimConfig.NovaCost)
            {
                CastNova();
            }
            if (input.WardQueued && _wardCooldown <= 0f && _charge >= SimConfig.WardCost)
            {
                CastWard();
            }
        }

        private void CastNova()
        {
            _charge -= SimConfig.NovaCost;
            _novaCooldown = SimConfig.NovaCooldown;
            _novaFlash = NovaFlashDuration;
            _novaX = _player.X;
            _novaY = _player.Y;
            _events |= SimEvents.NovaCast;

            for (int index = 0; index < _enemyCount; index += 1)
            {
                ref Enemy enemy = ref _enemies[index];
                if (enemy.State.Dead)
                {
                    continue;
                }
                float deltaX = enemy.State.X - _player.X;
                float deltaY = (enemy.State.Y - _player.Y) * SimConfig.IsoY;
                if (deltaX * deltaX + deltaY * deltaY <= SimConfig.NovaRadius * SimConfig.NovaRadius)
                {
                    DamageEnemy(ref enemy, SimConfig.NovaDamage);
                }
            }
        }

        private void CastWard()
        {
            _charge -= SimConfig.WardCost;
            _wardCooldown = SimConfig.WardCooldown;
            _player.WardTime = SimConfig.WardDuration;
            _events |= SimEvents.WardCast;
        }

        private void UpdateSkills(float deltaTime)
        {
            _novaCooldown = MathF.Max(0f, _novaCooldown - deltaTime);
            _wardCooldown = MathF.Max(0f, _wardCooldown - deltaTime);
            _novaFlash = MathF.Max(0f, _novaFlash - deltaTime);
            _player.WardTime = MathF.Max(0f, _player.WardTime - deltaTime);
            _charge = MathF.Min(SimConfig.LanternMax, _charge + SimConfig.LanternRegenPerSecond * deltaTime);
        }

        // --- Player ----------------------------------------------------------

        private void UpdatePlayer(float deltaTime, in SimInput input)
        {
            _player.AttackCooldown = MathF.Max(0f, _player.AttackCooldown - deltaTime);
            _player.DamageCooldown = MathF.Max(0f, _player.DamageCooldown - deltaTime);

            float movementX = input.MoveX;
            float movementY = input.MoveY;
            float movementLength = Hypot(movementX, movementY);

            if (movementLength > 0f)
            {
                movementX /= movementLength;
                movementY /= movementLength;
                float attackScale = _player.Action == ActorAction.Attack ? SimConfig.AttackMoveScale : 1f;
                _player.X += movementX * SimConfig.PlayerSpeed * attackScale * deltaTime;
                _player.Y += movementY * SimConfig.PlayerSpeed * SimConfig.YMoveScale * attackScale * deltaTime;
                _player.Moving = true;
                if (movementX != 0f)
                {
                    _player.Facing = movementX > 0f ? 1 : -1;
                }
                ClampToArena(ref _player.X, ref _player.Y, SimConfig.PlayerMarginClamp);
            }
            else
            {
                _player.Moving = false;
            }

            if (input.AttackQueued && _player.AttackCooldown <= 0f && _player.Action != ActorAction.Attack)
            {
                _player.AttackId += 1;
                _player.AttackCooldown = SimConfig.PlayerAttackCooldown;
                SetPlayerAction(ActorAction.Attack, true);
                _events |= SimEvents.PlayerStruck;
            }

            if (_player.Action != ActorAction.Attack)
            {
                SetPlayerAction(_player.Moving ? ActorAction.Move : ActorAction.Idle, false);
            }

            _player.ActionTime += deltaTime;

            int frame = -1;
            if (_player.Action == ActorAction.Attack)
            {
                frame = (int)MathF.Floor(_player.ActionTime * AttackClipFps);
                if (frame >= AttackClipFrames)
                {
                    SetPlayerAction(ActorAction.Idle, true);
                    frame = -1;
                }
            }

            if (frame < AttackActiveFirstFrame || frame > AttackActiveLastFrame)
            {
                return;
            }

            for (int index = 0; index < _enemyCount; index += 1)
            {
                ref Enemy enemy = ref _enemies[index];
                if (enemy.State.Dead || enemy.LastHitAttack == _player.AttackId)
                {
                    continue;
                }
                float deltaX = enemy.State.X - _player.X;
                float deltaY = (enemy.State.Y - _player.Y) * SimConfig.IsoY;
                bool inFacingArc = deltaX * _player.Facing >= SimConfig.FacingArcTolerance;
                if (inFacingArc && deltaX * deltaX + deltaY * deltaY <= SimConfig.PlayerAttackRange * SimConfig.PlayerAttackRange)
                {
                    enemy.LastHitAttack = _player.AttackId;
                    DamageEnemy(ref enemy, SimConfig.PlayerDamage);
                }
            }
        }

        private void SetPlayerAction(ActorAction action, bool force)
        {
            if (!force && _player.Action == action)
            {
                return;
            }
            _player.Action = action;
            _player.ActionTime = 0f;
        }

        private void DamagePlayer(float amount)
        {
            if (_mode == SimMode.GameOver || _player.DamageCooldown > 0f)
            {
                return;
            }

            // Ward refuses the damage outright but still burns the contact grace so a
            // warded player is not chain-hit by the same swing.
            if (_player.WardTime > 0f)
            {
                _player.DamageCooldown = SimConfig.PlayerHitGrace;
                return;
            }

            _player.DamageCooldown = SimConfig.PlayerHitGrace;
            _player.Health = MathF.Max(0f, _player.Health - amount);
            _events |= SimEvents.PlayerDamaged;

            if (_player.Health == 0f)
            {
                _mode = SimMode.GameOver;
                _reason = OverrunReason;
                SetPlayerAction(ActorAction.Die, true);
                _events |= SimEvents.GameOver;
            }
        }

        // --- Enemies ---------------------------------------------------------

        private void UpdateEnemies(float deltaTime)
        {
            for (int index = 0; index < _enemyCount; index += 1)
            {
                if (_enemies[index].State.Dead)
                {
                    _enemies[index].State.FadeTime -= deltaTime;
                    continue;
                }
                UpdateEnemy(index, deltaTime);
                if (_mode == SimMode.GameOver)
                {
                    break;
                }
            }

            for (int index = _enemyCount - 1; index >= 0; index -= 1)
            {
                if (_enemies[index].State.Dead && _enemies[index].State.FadeTime <= 0f)
                {
                    RemoveEnemyAt(index);
                }
            }
        }

        private void UpdateEnemy(int index, float deltaTime)
        {
            ref Enemy enemy = ref _enemies[index];
            enemy.AttackCooldown = MathF.Max(0f, enemy.AttackCooldown - deltaTime);

            float deltaX = _player.X - enemy.State.X;
            float deltaY = _player.Y - enemy.State.Y;
            float combatY = deltaY * SimConfig.IsoY;
            float distance = Hypot(deltaX, combatY);

            if (enemy.State.Action != ActorAction.Attack)
            {
                if (distance <= SimConfig.EnemyAttackRange && enemy.AttackCooldown <= 0f)
                {
                    enemy.DidDamage = false;
                    enemy.AttackCooldown = SimConfig.EnemyAttackCooldown
                        + MathF.Min(EnemyCooldownWaveCap, _wave * EnemyCooldownPerWave);
                    SetEnemyAction(ref enemy, ActorAction.Attack, true);
                }
                else
                {
                    float moveX = deltaX;
                    float moveY = deltaY;
                    float rawDistance = Hypot(moveX, moveY);
                    if (rawDistance > MoveEpsilon)
                    {
                        moveX /= rawDistance;
                        moveY /= rawDistance;
                    }

                    for (int otherIndex = 0; otherIndex < _enemyCount; otherIndex += 1)
                    {
                        if (otherIndex == index)
                        {
                            continue;
                        }
                        ref Enemy other = ref _enemies[otherIndex];
                        if (other.State.Dead)
                        {
                            continue;
                        }
                        float separationX = enemy.State.X - other.State.X;
                        float separationY = enemy.State.Y - other.State.Y;
                        float separationSquared = separationX * separationX + separationY * separationY;
                        if (separationSquared > SeparationMinDistanceSq
                            && separationSquared < SimConfig.SeparationRadius * SimConfig.SeparationRadius)
                        {
                            float separationDistance = MathF.Sqrt(separationSquared);
                            float separationWeight = (SimConfig.SeparationRadius - separationDistance) / SimConfig.SeparationRadius;
                            moveX += separationX / separationDistance * separationWeight * SimConfig.SeparationWeight;
                            moveY += separationY / separationDistance * separationWeight * SimConfig.SeparationWeight;
                        }
                    }

                    float adjustedLength = Hypot(moveX, moveY);
                    if (adjustedLength > MoveEpsilon)
                    {
                        moveX /= adjustedLength;
                        moveY /= adjustedLength;
                    }

                    float speed = SpeedFor(enemy.State.Id, enemy.State.IsBoss);
                    if (distance > SimConfig.EnemyAttackRange - EnemyChaseSlack)
                    {
                        enemy.State.X += moveX * speed * deltaTime;
                        enemy.State.Y += moveY * speed * SimConfig.YMoveScale * deltaTime;
                        ClampToArena(ref enemy.State.X, ref enemy.State.Y, SimConfig.EnemyMarginClamp);
                        // Run is reserved for bosses (SIM_SPEC animation action set).
                        SetEnemyAction(ref enemy, enemy.State.IsBoss ? ActorAction.Run : ActorAction.Move, false);
                    }
                    else
                    {
                        SetEnemyAction(ref enemy, ActorAction.Idle, false);
                    }
                }
            }

            if (MathF.Abs(deltaX) > EnemyFacingDeadzone)
            {
                enemy.State.Facing = deltaX > 0f ? 1 : -1;
            }

            enemy.State.ActionTime += deltaTime;

            int frame = -1;
            if (enemy.State.Action == ActorAction.Attack)
            {
                frame = (int)MathF.Floor(enemy.State.ActionTime * AttackClipFps);
                if (frame >= AttackClipFrames)
                {
                    SetEnemyAction(ref enemy, ActorAction.Idle, true);
                    frame = -1;
                }
            }

            if (frame < EnemyContactFrame || enemy.DidDamage)
            {
                return;
            }

            float contactX = _player.X - enemy.State.X;
            float contactY = (_player.Y - enemy.State.Y) * SimConfig.IsoY;
            float contactRange = SimConfig.EnemyAttackRange + SimConfig.EnemyContactBonus;
            if (contactX * contactX + contactY * contactY <= contactRange * contactRange)
            {
                enemy.DidDamage = true;
                float damage = MathF.Min(ContactDamageCap, ContactDamageBase + MathF.Floor(_wave * ContactDamagePerWave));
                if (enemy.State.IsBoss)
                {
                    damage *= SimConfig.BossDamageMul;
                }
                DamagePlayer(damage);
            }
        }

        private static void SetEnemyAction(ref Enemy enemy, ActorAction action, bool force)
        {
            if (!force && enemy.State.Action == action)
            {
                return;
            }
            enemy.State.Action = action;
            enemy.State.ActionTime = 0f;
        }

        private float SpeedFor(int enemyId, bool isBoss)
        {
            float speed = MathF.Min(
                EnemySpeedCap,
                EnemySpeedBase + _wave * EnemySpeedPerWave + enemyId % 3 * EnemySpeedIdStep);
            return isBoss ? speed * SimConfig.BossSpeedMul : speed;
        }

        private void DamageEnemy(ref Enemy enemy, float amount)
        {
            if (enemy.State.Dead)
            {
                return;
            }

            enemy.State.Health = MathF.Max(0f, enemy.State.Health - amount);
            _events |= SimEvents.EnemyHit;

            if (enemy.State.Health != 0f)
            {
                return;
            }

            enemy.State.Dead = true;
            enemy.State.FadeTime = SimConfig.EnemyFade;
            SetEnemyAction(ref enemy, ActorAction.Die, true);
            _livingEnemies -= 1;
            _score += (enemy.State.IsBoss ? BossKillScorePerWave : KillScorePerWave) * _wave;
            _kills += 1;
            _charge = MathF.Min(SimConfig.LanternMax, _charge + SimConfig.LanternChargePerKill);
            SpawnPickup(enemy.State.Id, enemy.State.IsBoss, enemy.State.X, enemy.State.Y);
            _events |= SimEvents.EnemyKilled;
        }

        private void RemoveEnemyAt(int index)
        {
            int tail = _enemyCount - index - 1;
            if (tail > 0)
            {
                Array.Copy(_enemies, index + 1, _enemies, index, tail);
            }
            _enemyCount -= 1;
            _enemies[_enemyCount] = default;
        }

        // --- Pickups ---------------------------------------------------------

        private void SpawnPickup(int enemyId, bool isBoss, float x, float y)
        {
            if (_pickupCount == _pickups.Length)
            {
                Array.Resize(ref _pickups, _pickups.Length * 2);
            }

            ref PickupState pickup = ref _pickups[_pickupCount];
            pickup.Id = _nextPickupId;
            // Bosses always drop the relic mote; ordinary drops rotate on enemy id.
            pickup.Kind = isBoss ? PickupKind.RelicMote : (PickupKind)(enemyId % 3);
            pickup.X = x;
            pickup.Y = y;
            pickup.Life = SimConfig.PickupLifetime;
            pickup.Bob = 0f;

            _pickupCount += 1;
            _nextPickupId += 1;
        }

        private void UpdatePickups(float deltaTime)
        {
            for (int index = _pickupCount - 1; index >= 0; index -= 1)
            {
                ref PickupState pickup = ref _pickups[index];
                pickup.Life -= deltaTime;
                pickup.Bob += deltaTime;

                float deltaX = _player.X - pickup.X;
                float deltaY = (_player.Y - pickup.Y) * SimConfig.IsoY;
                if (deltaX * deltaX + deltaY * deltaY <= SimConfig.PickupMagnetRadius * SimConfig.PickupMagnetRadius)
                {
                    CollectPickup(pickup.Kind);
                    RemovePickupAt(index);
                    continue;
                }

                if (pickup.Life <= 0f)
                {
                    RemovePickupAt(index);
                }
            }
        }

        private void CollectPickup(PickupKind kind)
        {
            if (kind == PickupKind.EmberShard)
            {
                _player.Health = MathF.Min(SimConfig.PlayerMaxHealth, _player.Health + SimConfig.EmberShardHeal);
            }
            else if (kind == PickupKind.OilFlask)
            {
                _charge = MathF.Min(SimConfig.LanternMax, _charge + SimConfig.OilFlaskCharge);
            }
            else
            {
                _relics += 1;
                _score += SimConfig.RelicScore;
            }
            _events |= SimEvents.PickupCollected;
        }

        private void RemovePickupAt(int index)
        {
            int tail = _pickupCount - index - 1;
            if (tail > 0)
            {
                Array.Copy(_pickups, index + 1, _pickups, index, tail);
            }
            _pickupCount -= 1;
            _pickups[_pickupCount] = default;
        }

        // --- Wave ------------------------------------------------------------

        private void StartWave(int waveNumber)
        {
            _wave = waveNumber;
            _waveSeed = waveNumber * 3 % SimConfig.SpawnPoints.Length;
            _pendingSpawns = SpawnCountForWave(waveNumber);
            _pendingBoss = IsBossWave(waveNumber);
            _spawnIndexInWave = 0;
            _spawnTimer = SimConfig.FirstSpawnDelay;
            _intermission = 0f;
            _mode = SimMode.Running;

            // The original only plays the wave cue from wave 2 on.
            if (waveNumber > 1)
            {
                _events |= SimEvents.WaveStarted;
            }
        }

        private void UpdateWave(float deltaTime)
        {
            if (_mode == SimMode.WaveClear)
            {
                _intermission -= deltaTime;
                if (_intermission <= 0f)
                {
                    StartWave(_wave + 1);
                }
                return;
            }

            if (_pendingSpawns > 0 && _enemyCount < SimConfig.EnemyCap)
            {
                _spawnTimer -= deltaTime;
                if (_spawnTimer <= 0f)
                {
                    bool boss = _pendingBoss;
                    _pendingBoss = false;
                    SpawnEnemy(boss);
                    _pendingSpawns -= 1;
                    _spawnTimer = MathF.Max(SpawnIntervalMin, SpawnIntervalBase - _wave * SpawnIntervalPerWave);
                }
            }

            if (_pendingSpawns == 0 && _livingEnemies == 0)
            {
                _intermission = SimConfig.WaveIntermission;
                _mode = SimMode.WaveClear;
            }
        }

        private void SpawnEnemy(bool boss)
        {
            if (_enemyCount == _enemies.Length)
            {
                Array.Resize(ref _enemies, _enemies.Length * 2);
            }

            int id = _nextEnemyId;
            float[] spawnPoint = SimConfig.SpawnPoints[SpawnPointIndexFor(_wave, id)];
            float health = SimConfig.EnemyBaseHealth
                + MathF.Min(EnemyHealthWaveCap, (_wave - 1) * EnemyHealthPerWave);
            if (boss)
            {
                health *= SimConfig.BossHealthMul;
            }

            ref Enemy enemy = ref _enemies[_enemyCount];
            enemy.State.Id = id;
            enemy.State.Visual = boss
                ? (_wave % BossVisualPeriod == 0 ? EnemyVisual.BossMonarch : EnemyVisual.BossCommander)
                : (EnemyVisual)((_wave + _spawnIndexInWave) % VisualRotation);
            enemy.State.X = spawnPoint[0];
            enemy.State.Y = spawnPoint[1];
            enemy.State.Facing = spawnPoint[0] < SimConfig.ArenaX ? 1 : -1;
            enemy.State.Health = health;
            enemy.State.MaxHealth = health;
            enemy.State.Dead = false;
            enemy.State.FadeTime = 0f;
            enemy.State.Action = ActorAction.Idle;
            enemy.State.ActionTime = 0f;
            enemy.State.IsBoss = boss;
            enemy.State.Scale = boss ? SimConfig.BossScale : 1f;
            enemy.AttackCooldown = id % 3 * FirstAttackDelayStep;
            enemy.DidDamage = false;
            enemy.LastHitAttack = -1;

            _enemyCount += 1;
            _nextEnemyId += 1;
            _spawnIndexInWave += 1;
            _livingEnemies += 1;

            if (boss)
            {
                _events |= SimEvents.BossSpawned;
            }
        }

        // --- Shared math -----------------------------------------------------

        private static void ClampToArena(ref float x, ref float y, float margin)
        {
            float halfWidth = SimConfig.ArenaHalfWidth - margin;
            float halfHeight = SimConfig.ArenaHalfHeight - margin * 0.5f;
            float localX = x - SimConfig.ArenaX;
            float localY = y - SimConfig.ArenaY;
            float normalized = MathF.Abs(localX) / halfWidth + MathF.Abs(localY) / halfHeight;

            if (normalized > 1f)
            {
                localX /= normalized;
                localY /= normalized;
                x = SimConfig.ArenaX + localX;
                y = SimConfig.ArenaY + localY;
            }
        }

        private static float Hypot(float x, float y) => MathF.Sqrt(x * x + y * y);

        private void Publish()
        {
            _enemyView.Clear();
            for (int index = 0; index < _enemyCount; index += 1)
            {
                _enemyView.Add(_enemies[index].State);
            }

            _pickupView.Clear();
            for (int index = 0; index < _pickupCount; index += 1)
            {
                _pickupView.Add(_pickups[index]);
            }
        }
    }
}
