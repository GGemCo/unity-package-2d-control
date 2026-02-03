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

        private Action<InputAction.CallbackContext> _onAttackPress;
        private Action<InputAction.CallbackContext> _onAttackRelease;
        private Action<InputAction.CallbackContext> _onJumpPress;
        private Action<InputAction.CallbackContext> _onJumpRelease;
        private Action<InputAction.CallbackContext> _onDashPress;
        private Action<InputAction.CallbackContext> _onDashRelease;
        private Action<InputAction.CallbackContext> _onInteractionPress;
        private Action<InputAction.CallbackContext> _onInteractionRelease;
        private Action<InputAction.CallbackContext> _onSimulationToolPress;
        private Action<InputAction.CallbackContext> _onSimulationToolRelease;

        public bool IsValid => _playerInput != null;

        public void Bind(
            PlayerInput playerInput,
            Action<InputAction.CallbackContext> onAttackPress,
            Action<InputAction.CallbackContext> onAttackRelease,
            Action<InputAction.CallbackContext> onJumpPress,
            Action<InputAction.CallbackContext> onJumpRelease,
            Action<InputAction.CallbackContext> onDashPress,
            Action<InputAction.CallbackContext> onDashRelease,
            Action<InputAction.CallbackContext> onInteractionPress,
            Action<InputAction.CallbackContext> onInteractionRelease,
            Action<InputAction.CallbackContext> onSimulationToolPress,
            Action<InputAction.CallbackContext> onSimulationToolRelease)
        {
            _playerInput = playerInput;
            if (_playerInput == null) return;

            // 이벤트 해제를 위해 참조 보관
            _onAttackPress = onAttackPress;
            _onAttackRelease = onAttackRelease;
            _onJumpPress = onJumpPress;
            _onJumpRelease = onJumpRelease;
            _onDashPress = onDashPress;
            _onDashRelease = onDashRelease;
            _onInteractionPress = onInteractionPress;
            _onInteractionRelease = onInteractionRelease;
            _onSimulationToolPress = onSimulationToolPress;
            _onSimulationToolRelease = onSimulationToolRelease;

            // 반드시 필요
            _playerInput.actions.Enable();

            Move = _playerInput.actions.FindAction(ConfigCommonControl.NameActionMove);
            Move?.Enable();

            Attack = _playerInput.actions.FindAction(ConfigCommonControl.NameActionAttack);
            if (Attack != null)
            {
                Attack.Enable();
                Attack.started += _onAttackPress;
                Attack.canceled += _onAttackRelease;
            }

            Jump = _playerInput.actions.FindAction(ConfigCommonControl.NameActionJump);
            if (Jump != null)
            {
                Jump.Enable();
                Jump.started += _onJumpPress;
                Jump.canceled += _onJumpRelease;
            }

            Dash = _playerInput.actions.FindAction(ConfigCommonControl.NameActionDash);
            if (Dash != null)
            {
                Dash.Enable();
                Dash.started += _onDashPress;
                Dash.canceled += _onDashRelease;
            }

            Interaction = _playerInput.actions.FindAction(ConfigCommonControl.NameActionInteraction);
            if (Interaction != null)
            {
                Interaction.Enable();
                Interaction.started += _onInteractionPress;
                Interaction.canceled += _onInteractionRelease;
            }

            SimulationTool = _playerInput.actions.FindAction(ConfigCommonControl.NameActionSimulationTool);
            if (SimulationTool != null)
            {
                SimulationTool.Enable();
                SimulationTool.started += _onSimulationToolPress;
                SimulationTool.canceled += _onSimulationToolRelease;
            }
        }

        public void Unbind()
        {
            if (_playerInput == null) return;

            if (Attack != null)
            {
                Attack.started -= _onAttackPress;
                Attack.canceled -= _onAttackRelease;
            }
            if (Jump != null)
            {
                Jump.started -= _onJumpPress;
                Jump.canceled -= _onJumpRelease;
            }
            if (Dash != null)
            {
                Dash.started -= _onDashPress;
                Dash.canceled -= _onDashRelease;
            }
            if (Interaction != null)
            {
                Interaction.started -= _onInteractionPress;
                Interaction.canceled -= _onInteractionRelease;
            }
            if (SimulationTool != null)
            {
                SimulationTool.started -= _onSimulationToolPress;
                SimulationTool.canceled -= _onSimulationToolRelease;
            }

            _playerInput = null;
            Move = null;
            Attack = null;
            Jump = null;
            Dash = null;
            Interaction = null;
            SimulationTool = null;

            _onAttackPress = null;
            _onAttackRelease = null;
            _onJumpPress = null;
            _onJumpRelease = null;
            _onDashPress = null;
            _onDashRelease = null;
            _onInteractionPress = null;
            _onInteractionRelease = null;
            _onSimulationToolPress = null;
            _onSimulationToolRelease = null;
        }
    }
}
