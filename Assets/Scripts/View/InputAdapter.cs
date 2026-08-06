// Polls the New Input System and merges keyboard + on-screen touch controls
// into one SimInput per fixed step. Key->boolean mapping is MODE-AWARE and
// owned here (docs/SIM_SPEC_HACKSLASH.md §2.3): the sim only trusts booleans.
//
//   Arena    : Q=Nova  E=Ward                     R=Restart  Space=Attack
//   Dungeon  : Q=Bolt  E=Pulse  R=Nova  F=Ward  G=Companion Hold
//              H=Companion Recall Shift=Dash Space=Combo
//              (restart is panel-button only — R is a skill)
using CinderCourt.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CinderCourt.View
{
    public sealed class InputAdapter : MonoBehaviour
    {
        /// <summary>View-owned input profile (mirrors sim GameMode).</summary>
        public enum Profile { Arena, Prologue, Dungeon }

        public Profile Mode = Profile.Arena;

        // Touch D-pad state (set by HudView's TouchButton components).
        // Kept as a keyboard-free fallback surface; the virtual joystick
        // (mobile spec #7) is the primary touch movement source.
        public bool TouchLeft, TouchRight, TouchUp, TouchDown;

        // Virtual joystick vector (set by HudView.VirtualJoystick). SIGN
        // CONVENTION: SimInput.MoveY is SCREEN-DOWN POSITIVE (SimTypes.cs L24
        // "screen-down positive, original convention"; the D-pad ▲ handler
        // does moveY -= 1). The joystick therefore stores +Y = drag DOWN on
        // screen, i.e. TouchMoveY = -uguiDragDelta.y / radius. Values are
        // pre-processed by the joystick (deadzone 0.15, re-normalized above
        // it) — the sim normalizes the merged vector anyway (CinderSim
        // L1013-1016), so direction is the only channel that matters.
        public float TouchMoveX, TouchMoveY;

        /// <summary>
        /// Returns true only when a dungeon modal consumed R as a stage retry.
        /// Normal dungeon play still maps R to Nova.
        /// </summary>
        public System.Func<bool> OnDungeonRetryShortcut;

        bool _attackLatch;
        bool _novaLatch;
        bool _wardLatch;
        bool _boltLatch;
        bool _pulseLatch;
        bool _dashLatch;
        bool _restartLatch;
        bool _companionHoldLatch;
        bool _companionRecallLatch;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            // Attack: hold-to-repeat (sim cooldown owns cadence in arena;
            // combo link window owns it in dungeon).
            if (keyboard.spaceKey.isPressed) _attackLatch = true;

            switch (Mode)
            {
                case Profile.Arena:
                    if (keyboard.qKey.wasPressedThisFrame) _novaLatch = true;
                    if (keyboard.eKey.wasPressedThisFrame) _wardLatch = true;
                    if (keyboard.rKey.wasPressedThisFrame) _restartLatch = true;
                    break;
                case Profile.Prologue:
                    if (keyboard.rKey.wasPressedThisFrame) _restartLatch = true;
                    break;
                case Profile.Dungeon:
                    if (keyboard.qKey.wasPressedThisFrame) _boltLatch = true;
                    if (keyboard.eKey.wasPressedThisFrame) _pulseLatch = true;
                    if (keyboard.rKey.wasPressedThisFrame &&
                        (OnDungeonRetryShortcut == null || !OnDungeonRetryShortcut()))
                        _novaLatch = true;
                    if (keyboard.fKey.wasPressedThisFrame) _wardLatch = true;
                    if (keyboard.leftShiftKey.wasPressedThisFrame ||
                        keyboard.rightShiftKey.wasPressedThisFrame) _dashLatch = true;
                    if (keyboard.gKey.wasPressedThisFrame) _companionHoldLatch = true;
                    if (keyboard.hKey.wasPressedThisFrame) _companionRecallLatch = true;
                    break;
            }
        }

        /// <summary>Queue calls from HUD buttons (touch/click).</summary>
        public void QueueAttack() => _attackLatch = true;
        /// <summary>§3: on-screen strike button hold state, so touch can charge.</summary>
        public bool TouchAttackHeld;
        int _growthLatch;
        /// <summary>§5: on-screen level-up choice tap (1..3).</summary>
        public void QueueGrowthChoice(int choice) => _growthLatch = choice;
        public void QueueNova() => _novaLatch = true;
        public void QueueWard() => _wardLatch = true;
        public void QueueBolt() => _boltLatch = true;
        public void QueuePulse() => _pulseLatch = true;
        public void QueueDash() => _dashLatch = true;
        public void QueueRestart() => _restartLatch = true;
        public void QueueCompanionHold() => _companionHoldLatch = true;
        public void QueueCompanionRecall() => _companionRecallLatch = true;
        /// <summary>Clears held on-screen movement when a modal removes its
        /// pointer targets. A captured finger will not necessarily receive
        /// PointerUp after the modal intercepts it.</summary>
        public void ClearTouchState()
        {
            TouchLeft = TouchRight = TouchUp = TouchDown = false;
            TouchMoveX = TouchMoveY = 0f;
        }
        /// <summary>Discards combat actions queued by touch targets hidden by a
        /// terminal modal without consuming the modal's restart/retry input.</summary>
        public void ClearCombatLatches()
        {
            _attackLatch = false;
            _novaLatch = false;
            _wardLatch = false;
            _boltLatch = false;
            _pulseLatch = false;
            _dashLatch = false;
            _companionHoldLatch = false;
            _companionRecallLatch = false;
            _growthLatch = 0;   // §5: a stuck latch would eat every later offer
        }



        /// <summary>True while the command console owns the keyboard — WASD and
        /// hotkeys must not fire mid-sentence. Latches set by UI buttons still
        /// pass (mobile can tap skills while typing is open).</summary>
        public bool TextInputActive;

        public SimInput Sample()
        {
            var keyboard = TextInputActive ? null : Keyboard.current;
            float moveX = 0f, moveY = 0f;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX += 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveY -= 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveY += 1f;
            }
            // Input depth §3/§5. AttackHeld is polled state, NOT a latch: the
            // sim needs to know the key is still down on every tick, and a
            // latch would clear after the first one. Touch keeps its own hold
            // flag so the on-screen strike button can charge too.
            var attackHeld = TouchAttackHeld
                || (keyboard != null && keyboard.spaceKey.isPressed);
            var growthChoice = 0;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) growthChoice = 1;
                else if (keyboard.digit2Key.wasPressedThisFrame) growthChoice = 2;
                else if (keyboard.digit3Key.wasPressedThisFrame) growthChoice = 3;
            }
            if (_growthLatch != 0)
            {
                growthChoice = _growthLatch;   // on-screen tap
            }

            if (TouchLeft) moveX -= 1f;
            if (TouchRight) moveX += 1f;
            if (TouchUp) moveY -= 1f;
            if (TouchDown) moveY += 1f;
            moveX += TouchMoveX;   // joystick (already deadzoned + normalized)
            moveY += TouchMoveY;   // screen-down positive, see field comment
            moveX = Mathf.Clamp(moveX, -1f, 1f);
            moveY = Mathf.Clamp(moveY, -1f, 1f);

            return new SimInput
            {
                MoveX = moveX,
                MoveY = moveY,
                AttackQueued = _attackLatch,
                NovaQueued = _novaLatch,
                WardQueued = _wardLatch,
                BoltQueued = _boltLatch,
                PulseQueued = _pulseLatch,
                DashQueued = _dashLatch,
                RestartQueued = _restartLatch,
                CompanionHoldQueued = _companionHoldLatch,
                CompanionRecallQueued = _companionRecallLatch,
                AttackHeld = attackHeld,
                GrowthChoice = growthChoice,
            };
        }

        /// <summary>Called by GameView after the tick batch consumed the sample.</summary>
        public void ClearLatches()
        {
            _attackLatch = false;
            _novaLatch = false;
            _wardLatch = false;
            _boltLatch = false;
            _pulseLatch = false;
            _dashLatch = false;
            _restartLatch = false;
            _companionHoldLatch = false;
            _companionRecallLatch = false;
            _growthLatch = 0;
        }
    }
}
