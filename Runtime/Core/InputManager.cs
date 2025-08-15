using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /*
     * Action
     *  - move   이동
     *  - attack 공격
     *  - ladder 사다리 오르 내리기
     *  - push   밀기, 끌기
     *  -        특정 위치에 걸쇠 걸기
     */

    /// <summary>
    /// Player Input Asset에 등록한 키보드, 마우스, 게임 패드등의 입력 처리
    /// Player 에 AddComponent 된다.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        private CharacterBase _characterBase;
        private CharacterBaseController _characterBaseController;
        
        // 입력 받기 
        private PlayerInput _playerInput;
        
        // 이동 처리
        private InputAction _inputActionMove;
        private ActionMove _actionMove;
        
        // 공격 처리
        private ActionAttack _actionAttack;
        
        private void Awake()
        {
            _characterBase = GetComponent<CharacterBase>();
            if (!_characterBase)
            {
                enabled = false;
                return;
            }
            _characterBaseController = GetComponent<CharacterBaseController>();

            InitializeControls();

            InitializeInputPlayer();
        }

        private void InitializeControls()
        {
            _actionAttack = new ActionAttack();
            _actionAttack.Initialize(this, _characterBase);
            
            _actionMove = new ActionMove();
            _actionMove.Initialize(this, _characterBase, _characterBaseController);
        }

        private void InitializeInputPlayer()
        {
            _playerInput = GetComponent<PlayerInput>();
            if (!_playerInput) return;
            // ★ 반드시 필요
            _playerInput.actions.Enable();
            _playerInput.actions["Move"].Enable();
            _playerInput.actions["Attack"].Enable();
            _inputActionMove = _playerInput.actions["Move"];
            var attack = _playerInput.actions["Attack"];
            attack.started += OnAttack;
            // attack.performed += OnAttack;
            // attack.canceled += OnAttack;
        }

        private void OnDestroy()
        {
            if (_actionAttack != null)
            {
                _actionAttack.OnDestroy();
            }
            if (_actionMove != null)
            {
                _actionMove.OnDestroy();
            }
            if (_playerInput)
            {
                var attackAction = _playerInput.actions["Attack"];
                attackAction.started -= OnAttack;
                // attackAction.performed -= OnAttack;
                // attackAction.canceled -= OnAttack;
            }
        }
        
        private void Update()
        {
            if (_characterBase.IsStatusAttack()) return;
            if (_characterBase.IsStatusAttackComboWait()) return;
            
            Vector2 move = _inputActionMove.ReadValue<Vector2>();
            if (move != Vector2.zero)
            {
                OnMoveContinuous(move);
            }
            else
            {
                _characterBase.Stop();
            }
        }
        private void OnMoveContinuous(Vector2 direction)
        {
            // 방향키 누르고 있는 동안 계속 호출됨
            // Debug.Log($"Moving: {direction}");
            _actionMove.Move(direction);
        }
        public void OnAttack(InputAction.CallbackContext ctx)
        {
            _actionAttack.Attack(ctx);
        }
    }
}