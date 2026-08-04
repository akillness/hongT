// Polls the New Input System and merges keyboard + on-screen touch controls
// into one SimInput per fixed step. Latched one-shot flags are consumed by
// GameView after each batch of ticks.
using CinderCourt.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CinderCourt.View
{
    public sealed class InputAdapter : MonoBehaviour
    {
        // Touch D-pad state (set by HudView's TouchButton components).
        public bool TouchLeft, TouchRight, TouchUp, TouchDown;

        bool _attackLatch;
        bool _novaLatch;
        bool _wardLatch;
        bool _restartLatch;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            // Attack: hold-to-repeat (original listens to browser key auto-repeat;
            // the sim's 0.48 s cooldown owns the cadence).
            if (keyboard.spaceKey.isPressed) _attackLatch = true;
            if (keyboard.qKey.wasPressedThisFrame) _novaLatch = true;
            if (keyboard.eKey.wasPressedThisFrame) _wardLatch = true;
            if (keyboard.rKey.wasPressedThisFrame) _restartLatch = true;
        }

        /// <summary>Queue calls from HUD buttons (touch/click).</summary>
        public void QueueAttack() => _attackLatch = true;
        public void QueueNova() => _novaLatch = true;
        public void QueueWard() => _wardLatch = true;
        public void QueueRestart() => _restartLatch = true;

        public SimInput Sample()
        {
            var keyboard = Keyboard.current;
            float moveX = 0f, moveY = 0f;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX += 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveY -= 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveY += 1f;
            }
            if (TouchLeft) moveX -= 1f;
            if (TouchRight) moveX += 1f;
            if (TouchUp) moveY -= 1f;
            if (TouchDown) moveY += 1f;
            moveX = Mathf.Clamp(moveX, -1f, 1f);
            moveY = Mathf.Clamp(moveY, -1f, 1f);

            return new SimInput
            {
                MoveX = moveX,
                MoveY = moveY,
                AttackQueued = _attackLatch,
                NovaQueued = _novaLatch,
                WardQueued = _wardLatch,
                RestartQueued = _restartLatch,
            };
        }

        /// <summary>Called by GameView after the tick batch consumed the sample.</summary>
        public void ClearLatches()
        {
            _attackLatch = false;
            _novaLatch = false;
            _wardLatch = false;
            _restartLatch = false;
        }
    }
}
