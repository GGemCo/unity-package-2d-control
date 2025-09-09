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
        /// <summary>
        /// Rigidbody를 사용하므로 FixedUpdate로 처리
        /// </summary>
        private void FixedUpdate()
        {
            // 1) 점프/낙하 상태 전이 및 착지 처리: 항상 호출
            //    - 점프 입력 유무와 관계없이 클리프 폴, 정점 전환, 착지 엔딩 등을 내부에서 처리
            _actionJump.Update();

            // 2) 전투/피격 등 제약 상태면 이동/입력 처리만 제한 (물리/낙하 전이는 위에서 이미 처리됨)
            if (_characterBase.IsStatusAttack()) return;
            if (_characterBase.IsStatusAttackComboWait()) return;
            if (_characterBase.IsStatusDamage()) return;

            // 3) 이동 입력 읽기
            Vector2 move = _inputActionMove.ReadValue<Vector2>();

            // 4) 점프/낙하 중 이동 처리
            if (_characterBase.IsStatusJump())
            {
                if (move != Vector2.zero)
                {
                    OnJumpMoveContinuous(move);
                }
                return;
            }

            // 5) 지상 이동/정지 처리
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