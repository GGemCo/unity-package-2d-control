using System.Linq;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
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
        // 올라가기/내려가기
        private ActionClimb _actionClimb;
        // 밀기/당기기
        private ActionPushPull _actionPushPull;

        private bool _canAttackPlayDashing;
        private bool _canMovePlayDashing;
        private bool _canJumpPlayDashing;
        
        private bool _canDashPlayJumping;
        
        private bool _canClimbingPlayJumping;
        private bool _canJumpPlayClimbing;
        private bool _canDashPlayClimbing;
        
        // === 추가 필드 ===
        private InteractionScanner2D _scanner;
        private InputAction _inputActionInteraction;
        private IInteraction _currentInteraction;
        private GGemCoPlayerActionSettings _playerActionSettings;
        
        private InputAction _inputActionAttack;
        private InputAction _inputActionJump;
        private InputAction _inputActionDash;
        
        private void Awake()
        {
            _characterBase = GetComponent<CharacterBase>();
            if (!_characterBase)
            {
                enabled = false;
                return;
            }

            _playerActionSettings = AddressableLoaderSettingsControl.Instance.playerActionSettings;
            if (_playerActionSettings)
            {
                ApplySettings();
#if UNITY_EDITOR
                // 플레이 중 인스펙터 수정 → 즉시 반영
                _playerActionSettings.Changed += ApplySettings;
#endif
            }

            _characterBaseController = GetComponent<CharacterBaseController>();

            
            _scanner = _characterBase.colliderHitArea.gameObject.GetComponent<InteractionScanner2D>();
            if (_scanner == null)
            {
                // 스캐너가 없으면 자동 추가(프로파일 편의를 위해)
                _scanner = _characterBase.colliderHitArea.gameObject.AddComponent<InteractionScanner2D>();
            }
            Player player = _characterBase as Player;
            player?.onEventDeadByEndGround.AddListener(OnDeadGround);
            
            InitializeControls();
            InitializeInputPlayer();
        }

        private void ApplySettings()
        {
            // _canMovePlayDashing = playerActionSettings.canMovePlayDashing;
            // true 일 경우 방향키를 누른 상태로 대시를 사용하면 대시를 하지 못 한다.
            _canMovePlayDashing = false;
            _canJumpPlayDashing = _playerActionSettings.canJumpPlayDashing;
            _canAttackPlayDashing = _playerActionSettings.canAttackPlayDashing;

            _canDashPlayJumping = _playerActionSettings.canDashPlayJumping;
                
            _canClimbingPlayJumping = _playerActionSettings.canClimbingPlayJumping;
            _canJumpPlayClimbing = _playerActionSettings.canJumpPlayClimbing;
            _canDashPlayClimbing = _playerActionSettings.canDashPlayClimbing;
        }

        private void InitializeControls()
        {
            _actionAttack = new ActionAttack();
            _actionAttack.Initialize(this, _characterBase, _characterBaseController);
            
            _actionMove = new ActionMove();
            _actionMove.Initialize(this, _characterBase, _characterBaseController);
            
            _actionJump = new ActionJump();
            _actionJump.Initialize(this, _characterBase, _characterBaseController);
            
            _actionDash = new ActionDash();
            _actionDash.Initialize(this, _characterBase, _characterBaseController);
            
            _actionClimb = new ActionClimb();
            _actionClimb.Initialize(this, _characterBase, _characterBaseController);
            _actionClimb.InteractionEnded += OnInteractionEnded;

            _actionPushPull = new ActionPushPull();
            _actionPushPull.Initialize(this, _characterBase, _characterBaseController);
            _actionPushPull.InteractionEnded += OnInteractionEnded;
            
            // 대시 진행 여부를 점프에 전달
            _actionJump.SetDashActiveQuery(() => _actionDash.IsDashing);
        }

        private void InitializeInputPlayer()
        {
            _playerInput = GetComponent<PlayerInput>();
            if (!_playerInput) return;
            // 반드시 필요
            _playerInput.actions.Enable();
            
            // 이동 액션
            _inputActionMove = _playerInput.actions.FindAction(ConfigCommonControl.NameActionMove);
            _inputActionMove?.Enable();

            // 공격 액션
            _inputActionAttack = _playerInput.actions.FindAction(ConfigCommonControl.NameActionAttack);
            if (_inputActionAttack != null)
            {
                _inputActionAttack.Enable();
                _inputActionAttack.started += OnAttack;
                // attack.performed += OnAttack;
                // attack.canceled += OnAttack;
            }
            
            // Jump 액션
            _inputActionJump = _playerInput.actions.FindAction(ConfigCommonControl.NameActionJump);
            if (_inputActionJump != null)
            {
                _inputActionJump.Enable();
                _inputActionJump.started += OnJump;
            }
            // Dash 액션 활성화
            _inputActionDash = _playerInput.actions.FindAction(ConfigCommonControl.NameActionDash);
            if (_inputActionDash != null)
            {
                _inputActionDash.Enable();
                _inputActionDash.started += OnDash;
            }
            // 맵 오브젝트 상호작용 활성화
            _inputActionInteraction = _playerInput.actions.FindAction(ConfigCommonControl.NameActionInteraction);
            if (_inputActionInteraction != null)
            {
                _inputActionInteraction.started += OnInteraction;
            }
            
            // _playerInput.SwitchCurrentControlScheme("sss");
            if (_playerInput != null)
                _playerInput.onControlsChanged += OnChangeControlScheme;
        }

        private void OnDestroy()
        {
            _actionAttack?.OnDestroy();
            _actionMove?.OnDestroy();
            _actionJump?.OnDestroy();
            _actionDash?.OnDestroy();

            if (_actionClimb != null)
            {
                _actionClimb.OnDestroy();
                _actionClimb.InteractionEnded -= OnInteractionEnded;
            }

            if (_actionPushPull != null)
            {
                _actionPushPull.OnDestroy();
                _actionPushPull.InteractionEnded -= OnInteractionEnded;
            }

            if (_inputActionAttack != null)
                _inputActionAttack.started -= OnAttack;
            if (_inputActionJump != null)
                _inputActionJump.started -= OnJump;
            if (_inputActionDash != null)
                _inputActionDash.started -= OnDash;
            if (_inputActionInteraction != null)
                _inputActionInteraction.started -= OnInteraction;
            
            if (_playerActionSettings)
            {
#if UNITY_EDITOR
                // 플레이 중 인스펙터 수정 → 즉시 반영
                _playerActionSettings.Changed -= ApplySettings;
#endif
            }
            
            Player player = _characterBase as Player;
            player?.onEventDeadByEndGround.RemoveAllListeners();
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
            
            // 2) 이동 입력 읽기
            // ActionClimb 에서 사용하고 있음
            // ActionPushPull 에서 사용하고 있음
            Vector2 move = _inputActionMove.ReadValue<Vector2>();
            
            _actionClimb.Update();
            _actionPushPull.Update();

            // 3) 전투/피격 등 제약 상태면 이동/입력 처리만 제한 (물리/낙하 전이는 위에서 이미 처리됨)
            if (_characterBase.IsStatusAttack()) return;
            if (_characterBase.IsStatusAttackComboWait()) return;
            if (_characterBase.IsStatusDamage()) return;
            if (_characterBase.IsStatusClimb()) return;
            if (_characterBase.IsStatusPush()) return;

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
                // 대시 중 공격 가능
                if (_canAttackPlayDashing)
                {
                    _actionDash.CancelDash(true);
                }
                // 대시 중 공격 불가능
                else
                {
                    GcLogger.Log($"PlayerAction 셋팅에 canAttackPlayDashing 값이 false 입니다.");
                    return;
                }
            }
            // 점프 중 공격 불가능
            else if (_characterBase.IsStatusJump() && _actionJump.IsJumping)
            {
                GcLogger.Log($"점프 중 공격은 불가능 합니다.");
                return;
            }
            // 등반 중 공격 불가능
            else if (_characterBase.IsStatusClimb() && _actionClimb.IsClimbing)
            {
                GcLogger.Log($"등반 중 공격은 불가능 합니다.");
                return;
            }
            // 밀기 중 공격 불가능
            else if (_characterBase.IsStatusPush() && _actionPushPull.IsPushing)
            {
                GcLogger.Log($"밀기 중 공격은 불가능 합니다.");
                return;
            }
            _actionAttack.Attack(ctx);
        }
        public void OnJump(InputAction.CallbackContext ctx)
        {
            if (_characterBase.IsStatusDash() && _actionDash.IsDashing)
            {
                // 대시 중 점프 가능
                if (_canJumpPlayDashing)
                {
                    _actionDash.CancelDash(true);
                }
                // 대시 중 점프 불가능
                else
                {
                    GcLogger.Log($"PlayerAction 셋팅에 canJumpPlayDashing 값이 false 입니다.");
                    return;
                }
            }
            else if (_characterBase.IsStatusClimb() && _actionClimb.IsClimbing)
            {
                // 사다리에서 점프 가능
                if (_canJumpPlayClimbing)
                {
                    _actionClimb.CancelClimb();
                }
                // 사다리에서 점프 불가능
                else
                {
                    GcLogger.Log($"PlayerAction 셋팅에 canJumpPlayClimbing 값이 false 입니다.");
                    return;
                }
            }
            // 밀기 중 점프 불가능
            else if (_characterBase.IsStatusPush() && _actionPushPull.IsPushing)
            {
                GcLogger.Log($"밀기 중 점프는 불가능 합니다.");
                return;
            }
            _actionJump.Jump(ctx);
        }
        public void OnDash(InputAction.CallbackContext ctx)
        {
            if (_characterBase.IsStatusJump())
            {
                // 점프 중 대시 가능
                if (_canDashPlayJumping)
                {
                    // 애니메이션 없이 즉시 종료(피격/경직 등 강제 취소), 중력 복구도 스킵 가능
                    if (_actionJump.IsJumping)
                        _actionJump.CancelJump(skipLandAnimation: true, restoreGravity: false);
                }
                // 점프 중 대시 불가능
                else
                {
                    GcLogger.Log($"PlayerAction 셋팅에 canDashPlayJumping 값이 false 입니다.");
                    return;
                }
            }
            else if (_characterBase.IsStatusClimb() && _actionClimb.IsClimbing)
            {
                // 사다리에서 점프 가능
                if (_canDashPlayClimbing)
                {
                    var dir = _inputActionMove.ReadValue<Vector2>();
                    _characterBase.SetFacing(dir);
                    _actionClimb.CancelClimb();
                }
                // 사다리에서 점프 불가능
                else
                {
                    GcLogger.Log($"PlayerAction 셋팅에 canDashPlayClimbing 값이 false 입니다.");
                    return;
                }
            }
            // 밀기 중 대시 불가능
            else if (_characterBase.IsStatusPush() && _actionPushPull.IsPushing)
            {
                GcLogger.Log($"밀기 중 대시는 불가능 합니다.");
                return;
            }

            _actionDash.Dash(ctx);
        }
        /// <summary>
        /// F 입력 처리: 가장 우선순위 높은 상호작용 대상 선택 → Begin/End 토글
        /// </summary>
        private void OnInteraction(InputAction.CallbackContext ctx)
        {
            // 0) 대시/점프/공격 중 상호작용을 제한하고 싶다면 여기서 리턴
            if (_characterBase.IsStatusDash() || _characterBase.IsStatusAttack())
            {
                return;
            }

            // 1) 이미 상호작용 중이면 종료
            if (_currentInteraction != null)
            {
                _currentInteraction.EndInteract(gameObject);
                _currentInteraction = null;
                
                return;
            }

            // 2) 후보 중 사용 가능 & 최상위 우선순위 선택
            var best = _scanner.Candidates
                .Where(c => c != null && c.IsAvailable(gameObject))
                .OrderBy(c => c.Priority)
                .FirstOrDefault();

            if (best == null)
            {
                GcLogger.Log($"scanner에 object가 없습니다.");
                return;
            }

            // 3) BeginInteract 성공 시 현재 상호작용으로 고정
            if (best.BeginInteract(gameObject))
            {
                _currentInteraction = best;
            }
        }

        // === ActionLadder/PushPull 과의 연결 API ===

        public bool TryBeginLadder(ObjectClimb climb)
        {
            // 상충 상태 정리
            if (_actionDash.IsDashing) _actionDash.CancelDash(true);
            if (_characterBase.IsStatusJump() && _actionJump.IsJumping)
            {
                if (_canClimbingPlayJumping)
                {
                    _actionJump.CancelJump(skipLandAnimation: true, restoreGravity: true);
                }
                else
                {
                    GcLogger.Log($"PlayerAction 셋팅에 canClimbingPlayJumping 값이 false 입니다.");
                    return false;
                }
            }

            return _actionClimb.Begin(climb);
        }

        public void EndLadder(ObjectClimb climb)
        {
            _actionClimb.End(climb);
            if ((ObjectClimb)_currentInteraction == climb) _currentInteraction = null; // 안전망
        }

        public bool TryBeginPushPull(ObjectPushPull target)
        {
            if (_actionDash.IsDashing) _actionDash.CancelDash(true);
            // 점프 중에는 밀기/당기기 금지(필요 시 조건 수정)
            if (_characterBase.IsStatusJump()) return false;

            return _actionPushPull.Begin(target);
        }

        public void EndPushPull(ObjectPushPull target)
        {
            _actionPushPull.End(target);
            if ((ObjectPushPull)_currentInteraction == target) _currentInteraction = null; // 안전망
        }
        private void OnChangeControlScheme(PlayerInput playerInput)
        {
            // GcLogger.Log($"on controls changed. {playerInput.currentControlScheme}");
            // var uiPanelControl = ControlPackageManager.Instance.GetUIPanelControl();
            // if (!uiPanelControl) return;
            // uiPanelControl.SetScheme(playerInput.currentControlScheme);
        }
        
        private void OnInteractionEnded(IInteraction ended)
        {
            // 현재 상호작용 중인 대상과 같다면 초기화
            if (_currentInteraction == ended)
                _currentInteraction = null;
        }
        /// <summary>
        /// 캐릭터가 바닥을 벗어나서 사망했을 때
        /// </summary>
        private void OnDeadGround()
        {
            _actionJump?.CancelJump(true);
            _actionDash?.CancelDash(true);
            _actionClimb?.CancelClimb();
            _actionPushPull?.Cancel();
        }
    }
}