using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// PlayerInput(Input System)로부터 InputAction을 찾아 캐시하고,
    /// Enable/Disable 및 콜백 바인딩/해제를 표준화합니다.
    /// </summary>
    internal sealed class PlayerInputBindings
    {
        public InputAction Move { get; private set; }
        public InputAction Attack { get; private set; }
        public InputAction Jump { get; private set; }
        public InputAction Dash { get; private set; }
        public InputAction Interaction { get; private set; }
        public InputAction SimulationTool { get; private set; }

        private PlayerInput _playerInput;

        private Action<InputAction.CallbackContext> _onAttackStarted;
        private Action<InputAction.CallbackContext> _onJumpStarted;
        private Action<InputAction.CallbackContext> _onDashStarted;
        private Action<InputAction.CallbackContext> _onInteractionStarted;
        private Action<InputAction.CallbackContext> _onSimulationToolPerformed;

        public bool IsValid => _playerInput != null;

        public void Bind(
            PlayerInput playerInput,
            Action<InputAction.CallbackContext> onAttackStarted,
            Action<InputAction.CallbackContext> onJumpStarted,
            Action<InputAction.CallbackContext> onDashStarted,
            Action<InputAction.CallbackContext> onInteractionStarted,
            Action<InputAction.CallbackContext> onSimulationToolPerformed)
        {
            _playerInput = playerInput;
            if (_playerInput == null) return;

            // 이벤트 해제를 위해 참조 보관
            _onAttackStarted = onAttackStarted;
            _onJumpStarted = onJumpStarted;
            _onDashStarted = onDashStarted;
            _onInteractionStarted = onInteractionStarted;
            _onSimulationToolPerformed = onSimulationToolPerformed;

            // 반드시 필요
            _playerInput.actions.Enable();

            Move = _playerInput.actions.FindAction(ConfigCommonControl.NameActionMove);
            Move?.Enable();

            Attack = _playerInput.actions.FindAction(ConfigCommonControl.NameActionAttack);
            if (Attack != null)
            {
                Attack.Enable();
                Attack.started += _onAttackStarted;
            }

            Jump = _playerInput.actions.FindAction(ConfigCommonControl.NameActionJump);
            if (Jump != null)
            {
                Jump.Enable();
                Jump.started += _onJumpStarted;
            }

            Dash = _playerInput.actions.FindAction(ConfigCommonControl.NameActionDash);
            if (Dash != null)
            {
                Dash.Enable();
                Dash.started += _onDashStarted;
            }

            Interaction = _playerInput.actions.FindAction(ConfigCommonControl.NameActionInteraction);
            if (Interaction != null)
            {
                Interaction.Enable();
                Interaction.started += _onInteractionStarted;
            }

            SimulationTool = _playerInput.actions.FindAction(ConfigCommonControl.NameActionSimulationTool);
            if (SimulationTool != null)
            {
                SimulationTool.Enable();
                SimulationTool.performed += _onSimulationToolPerformed;
            }
        }

        public void Unbind()
        {
            if (_playerInput == null) return;

            if (Attack != null)
                Attack.started -= _onAttackStarted;
            if (Jump != null)
                Jump.started -= _onJumpStarted;
            if (Dash != null)
                Dash.started -= _onDashStarted;
            if (Interaction != null)
                Interaction.started -= _onInteractionStarted;
            if (SimulationTool != null)
                SimulationTool.performed -= _onSimulationToolPerformed;

            _playerInput = null;
            Move = null;
            Attack = null;
            Jump = null;
            Dash = null;
            Interaction = null;
            SimulationTool = null;

            _onAttackStarted = null;
            _onJumpStarted = null;
            _onDashStarted = null;
            _onInteractionStarted = null;
            _onSimulationToolPerformed = null;
        }
    }
}
