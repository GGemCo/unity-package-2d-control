using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /*
     * Action
     *  - move   이동
     *  - attack 공격
     *  - dash   대시
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
        // 점프
        private ActionJump _actionJump;
        // 대시
        private ActionDash _actionDash;

        private bool _canAttackPlayDashing;
        private bool _canMovePlayDashing;
        private bool _canJumpPlayDashing;
        
        private bool _canDashPlayJumping;
        
        private void Awake()
        {
            _characterBase = GetComponent<CharacterBase>();
            if (!_characterBase)
            {
                enabled = false;
                return;
            }

            var playerActionSettings = AddressableLoaderSettingsControl.Instance.playerActionSettings;
            if (playerActionSettings)
            {
                _canMovePlayDashing = playerActionSettings.canMovePlayDashing;
                _canJumpPlayDashing = playerActionSettings.canJumpPlayDashing;
                _canAttackPlayDashing = playerActionSettings.canAttackPlayDashing;

                _canDashPlayJumping = playerActionSettings.canDashPlayJumping;
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
            
            _actionDash = new ActionDash();
            _actionDash.Initialize(this, _characterBase, _characterBaseController);
            
            // 대시 진행 여부를 점프에 전달
            _actionJump.SetDashActiveQuery(() => _actionDash.IsDashing);
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
            _playerInput.actions[ConfigCommonControl.NameActionDash].Enable(); // Dash 액션 활성화
            
            _inputActionMove = _playerInput.actions[ConfigCommonControl.NameActionMove];
            var attack = _playerInput.actions[ConfigCommonControl.NameActionAttack];
            attack.started += OnAttack;
            // attack.performed += OnAttack;
            // attack.canceled += OnAttack;

            var jump = _playerInput.actions[ConfigCommonControl.NameActionJump];
            jump.started += OnJump;
            
            var dash = _playerInput.actions[ConfigCommonControl.NameActionDash];
            dash.started += OnDash;
            
            // _playerInput.SwitchCurrentControlScheme("sss");
            _playerInput.onControlsChanged += OnChangeControlScheme;
        }

        private void OnDestroy()
        {
            _actionAttack?.OnDestroy();
            _actionMove?.OnDestroy();
            _actionJump?.OnDestroy();
            _actionDash?.OnDestroy();
            
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
            _actionDash.Update();

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
            // 5) 대시 중 이동 처리
            if (_characterBase.IsStatusDash())
            {
                // 이동 키를 조작했을 때, 땅에 있을때만 이동하기
                if (_canMovePlayDashing && move != Vector2.zero && _actionJump.IsGroundedByCollision())
                {
                    // 피격/경직 등으로 즉시 끊고 싶을 때(애니메이션 스킵)
                    if (_actionDash.IsDashing)
                        _actionDash.CancelDash(skipEndAnimation: true);
                }
                return;
            }

            // 6) 지상 이동/정지 처리
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
            if (_characterBase.IsStatusDash() && _actionDash.IsDashing)
            {
                // 대시 중 공격이 가능하면
                if (_canAttackPlayDashing)
                {
                    _actionDash.CancelDash(true);
                }
                // 대시 중 공격이 불가능하면
                else
                {
                    return;
                }
            }

            if (_characterBase.IsStatusJump() && _actionJump.IsJumping)
            {
                // 점프 중 공격 불가능
                return;
            }
            _actionAttack.Attack(ctx);
        }
        public void OnJump(InputAction.CallbackContext ctx)
        {
            if (_characterBase.IsStatusDash() && _actionDash.IsDashing)
            {
                // 대시 중 점프가 가능하면
                if (_canJumpPlayDashing)
                {
                    _actionDash.CancelDash(true);
                }
                // 대시 중 점프가 불가능하면
                else
                {
                    return;
                }
            }
            _actionJump.Jump(ctx);
        }
        public void OnDash(InputAction.CallbackContext ctx)
        {
            if (_characterBase.IsStatusJump())
            {
                // 점프 중 대시가 가능하면
                if (_canDashPlayJumping)
                {
                    // 애니메이션 없이 즉시 종료(피격/경직 등 강제 취소), 중력 복구도 스킵 가능
                    if (_actionJump.IsJumping)
                        _actionJump.CancelJump(skipLandAnimation: true, restoreGravity: false);
                }
                // 점프 중 대시가 불가능하면
                else
                {
                    return;
                }
            }

            _actionDash.Dash(ctx);
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