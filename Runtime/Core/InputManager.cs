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
        private ActionJump _actionJump;
        
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
            
            _actionJump = new ActionJump();
            _actionJump.Initialize(this, _characterBase, _characterBaseController);
        }

        private void InitializeInputPlayer()
        {
            _playerInput = GetComponent<PlayerInput>();
            if (!_playerInput) return;
            // 반드시 필요
            _playerInput.actions.Enable();
            _playerInput.actions[ConfigCommonControl.NameActionMove].Enable();
            _playerInput.actions[ConfigCommonControl.NameActionAttack].Enable();
            _playerInput.actions[ConfigCommonControl.NameActionJump].Enable(); // Jump 액션 활성화
            
            _inputActionMove = _playerInput.actions[ConfigCommonControl.NameActionMove];
            var attack = _playerInput.actions[ConfigCommonControl.NameActionAttack];
            attack.started += OnAttack;
            // attack.performed += OnAttack;
            // attack.canceled += OnAttack;

            var jump = _playerInput.actions[ConfigCommonControl.NameActionJump];
            jump.started += OnJump;
            
            // _playerInput.SwitchCurrentControlScheme("sss");
            _playerInput.onControlsChanged += OnChangeControlScheme;
        }

        private void OnDestroy()
        {
            _actionAttack?.OnDestroy();
            _actionMove?.OnDestroy();
            _actionJump?.OnDestroy();
            
            if (_playerInput)
            {
                var attackAction = _playerInput.actions[ConfigCommonControl.NameActionAttack];
                attackAction.started -= OnAttack;
                // attackAction.performed -= OnAttack;
                // attackAction.canceled -= OnAttack;
            }
        }
        
        private void Update()
        {
            if (_characterBase.IsStatusAttack()) return;
            if (_characterBase.IsStatusAttackComboWait()) return;
            if (_characterBase.IsStatusDamage()) return;
            
            Vector2 move = _inputActionMove.ReadValue<Vector2>();
            
            if (_characterBase.IsStatusJump())
            {
                _actionJump.Update(); // 점프 상태 업데이트
                if (move != Vector2.zero)
                {
                    OnJumpMoveContinuous(move);
                }
                return;
            }
            
            if (move != Vector2.zero)
            {
                OnMoveContinuous(move);
            }
            else
            {
                _characterBase.Stop();
            }
        }
        private void OnJumpMoveContinuous(Vector2 direction)
        {
            // 방향키 누르고 있는 동안 계속 호출됨
            // Debug.Log($"Moving: {direction}");
            _actionMove.JumpMove(direction);
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
        public void OnJump(InputAction.CallbackContext ctx)
        {
            _actionJump.Jump(ctx);
        }

        private void OnChangeControlScheme(PlayerInput playerInput)
        {
            // GcLogger.Log($"on controls changed. {playerInput.currentControlScheme}");
            // var uiPanelControl = ControlPackageManager.Instance.GetUIPanelControl();
            // if (!uiPanelControl) return;
            // uiPanelControl.SetScheme(playerInput.currentControlScheme);
        }
    }
}