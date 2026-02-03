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

        // InputAction 캐시/바인딩 전담
        private PlayerInputBindings _bindings;

        // 이동 처리
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
        
        // 시뮬레이션 툴 사용
        private IToolAction _toolAction;

        // === Wall Action (Phase 기반 통합) ===
        private ActionWall _actionWall;

        private bool _canAttackPlayDashing;
        private bool _canMovePlayDashing;
        private bool _canJumpPlayDashing;
        
        private bool _canDashPlayJumping;
        
        private bool _canClimbingPlayJumping;
        private bool _canJumpPlayClimbing;
        private bool _canDashPlayClimbing;
        
        private bool _canJumpUseSkill;
        private bool _canDashUseSkill;
        private bool _canAttackPlayJump;

        // 정책/핸들러
        private PlayerInputPolicy _policy;
        private AttackInputHandler _attackHandler;
        private JumpInputHandler _jumpHandler;
        private DashInputHandler _dashHandler;
        
        // === 추가 필드 ===
        private InteractionScanner2D _scanner;
        private InteractionInputHandler _interactionHandler;
        private GGemCoPlayerActionSettings _playerActionSettings;

        // Simulation Tool: UI 위 클릭 방지(다음 프레임에서 판정)
        private SimulationToolInputHandler _simulationToolHandler;

        // AutoMove(Core)
        private AutoMoveAdapter _autoMove;
        
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

            // AutoMove: 이동 벡터 오버라이드/입력 잠금 + Suspend 관리
            _autoMove = new AutoMoveAdapter(
                GetComponent<IAutoMoveVectorProvider>(),
                GetComponent<IAutoMoveSuspendService>());

            if (_characterBase.colliderHitArea)
            {
                _scanner = _characterBase.colliderHitArea.gameObject.GetComponent<InteractionScanner2D>();
                if (_scanner == null)
                {
                    // 스캐너가 없으면 자동 추가(프로파일 편의를 위해)
                    _scanner = _characterBase.colliderHitArea.gameObject.AddComponent<InteractionScanner2D>();
                }

                _interactionHandler = new InteractionInputHandler(_scanner);
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
            
            _canJumpUseSkill = _playerActionSettings.canJumpUseSkill;
            _canDashUseSkill = _playerActionSettings.canDashUseSkill;
            _canAttackPlayJump = _playerActionSettings.canAttackPlayJump;

            // Policy snapshot 갱신
            if (_policy != null)
            {
                _policy.CanAttackPlayDashing = _canAttackPlayDashing;
                _policy.CanJumpPlayDashing = _canJumpPlayDashing;
                _policy.CanDashPlayJumping = _canDashPlayJumping;
                _policy.CanClimbingPlayJumping = _canClimbingPlayJumping;
                _policy.CanJumpPlayClimbing = _canJumpPlayClimbing;
                _policy.CanDashPlayClimbing = _canDashPlayClimbing;
                _policy.CanJumpUseSkill = _canJumpUseSkill;
                _policy.CanDashUseSkill = _canDashUseSkill;
                _policy.CanAttackPlayJump = _canAttackPlayJump;
            }
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

            // Wall Action (Phase 기반)
            _actionWall = new ActionWall();
            _actionWall.Initialize(this, _characterBase, _characterBaseController);

#if UNITY_EDITOR
            // Wall Action 디버그 Gizmo(레이 캐스트) 렌더링 프록시 바인딩
            if (_playerActionSettings.enableWallDebugGizmos)
            {
                var wallDebug = GetComponent<ActionWallDebugDrawer>();
                if (wallDebug == null)
                {
                    wallDebug = ControlPackageManager.Instance.gameObject.AddComponent<ActionWallDebugDrawer>();
                }

                wallDebug.Bind(_actionWall, _playerActionSettings);
            }
#endif
            // 벽 액션 진행 여부를 점프에 전달(클리프 폴/착지 전이 충돌 방지)
            _actionJump.SetWallActionActiveQuery(() => _actionWall != null && (_actionWall.IsWallLocked || _actionWall.IsKinematicWallJumping));

            // Simulation Tool 입력 핸들러(다음 프레임 UI 판정)
            _simulationToolHandler = new SimulationToolInputHandler(
                this,
                _characterBase,
                _actionDash,
                _actionJump,
                _actionClimb,
                _actionPushPull,
                () => _actionWall is { IsWallLocked: true });

            // === Input Policy / Handlers ===
            _policy = new PlayerInputPolicy(
                _characterBase,
                _actionDash,
                _actionJump,
                _actionClimb,
                _actionPushPull,
                _actionWall,
                () => _characterBase != null ? _characterBase.GetComponent<ISkillCancelableDriver>() : null,
                () => _bindings?.Move?.ReadValue<Vector2>() ?? Vector2.zero);

            // Settings 적용 값 주입
            ApplySettings();

            _attackHandler = new AttackInputHandler(_characterBase, _actionAttack, _policy);
            _jumpHandler = new JumpInputHandler(_characterBase, _actionJump, _actionWall, _policy);
            _dashHandler = new DashInputHandler(_characterBase, _actionDash, _policy);
        }

        private void InitializeInputPlayer()
        {
            _playerInput = GetComponent<PlayerInput>();
            if (!_playerInput) return;
            _bindings = new PlayerInputBindings();
            _bindings.Bind(
                _playerInput,
                OnAttack,
                OnJump,
                OnDash,
                OnInteraction,
                OnSimulationTool);
            
            if (_playerInput != null)
                _playerInput.onControlsChanged += OnChangeControlScheme;
        }

        private void OnDestroy()
        {
            _actionAttack?.OnDestroy();
            _actionMove?.OnDestroy();
            _actionJump?.OnDestroy();
            _actionDash?.OnDestroy();

            _actionWall?.OnDestroy();

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

            if (_playerInput != null)
                _playerInput.onControlsChanged -= OnChangeControlScheme;

            _bindings?.Unbind();
            _bindings = null;

            // Suspend 누락 방지
            _autoMove?.ReleaseAll();

            _toolAction?.Cancel();
            _toolAction?.OnDestroy();
            _toolAction = null;
            
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
        private void Update()
        {
            _simulationToolHandler?.Tick();
        }

        private void OnDisable()
        {
            // Wall Action 등에서 Suspend를 쥔 상태로 비활성화될 수 있으므로, 누락 없이 해제한다.
            _autoMove?.ReleaseAll();
        }

        private void UpdateAutoMoveSuspendByWall()
        {
            if (_autoMove == null || _actionWall == null) return;
            bool wallActive = _actionWall.IsWallLocked || _actionWall.IsKinematicWallJumping;
            _autoMove.TickSuspendByWall(wallActive);
        }
        /// <summary>
        /// Rigidbody를 사용하므로 FixedUpdate로 처리
        /// </summary>
        private void FixedUpdate()
        {
            if (_characterBase.IsStatusDead()) return;
            
            // todo. 정리 필요
            if (_characterBase.IsStatusCastingSkill()) return;
            if (_characterBase.IsStatusUseSkill()) return;

            // Wall Action 진행 중에는 AutoMove를 Pause 한다.
            UpdateAutoMoveSuspendByWall();
            
            // === Kinematic Wall Jump 우선 처리 ===
            // 벽 점프를 Kinematic으로 처리하는 동안에는 기존 Jump/Move 시스템이 물리값을 덮어쓰지 않도록 한다.
            if (_actionWall is { IsKinematicWallJumping: true })
            {
                // Kinematic Wall Jump가 진행 중이면, 다른 물리 로직이 덮어쓰지 않도록 우선 처리한다.
                Vector2 rawMoveForWallJump = _bindings?.Move?.ReadValue<Vector2>() ?? Vector2.zero;

                Vector2 moveForWallJump = _autoMove?.ResolveMove(rawMoveForWallJump) ?? rawMoveForWallJump;

                _actionWall.FixedTick(moveForWallJump);
                return;
            }
            // 1) 점프/낙하 상태 전이 및 착지 처리: 항상 호출
            //    - 점프 입력 유무와 관계없이 클리프 폴, 정점 전환, 착지 엔딩 등을 내부에서 처리
            _actionJump.Update();
            _actionDash.Update();
            
            // 2) 이동 입력 읽기
            // ActionClimb 에서 사용하고 있음
            // ActionPushPull 에서 사용하고 있음
            Vector2 rawMove = _bindings?.Move?.ReadValue<Vector2>() ?? Vector2.zero;

            // AutoMove: 활성화된 경우 이동 벡터를 오버라이드한다.
            // - 수동 입력이 들어오면 Core(Provider)에게 통지하여 취소 정책을 적용할 수 있다.
            Vector2 move = _autoMove?.ResolveMove(rawMove) ?? rawMove;

            // 3) Wall Action 업데이트(공중에서만 작동)
            _actionWall?.FixedTick(move);

            // Hang/Slide 중에는 일반 이동/점프 이동 처리를 막는다(벽 고정/슬라이드가 우선)
            if (_actionWall is { IsWallLocked: true })
            {
                // 공격/대시 등 다른 입력은 OnAttack/OnDash에서 별도 정책으로 처리한다.
                return;
            }

            // 4) 기타 상호작용/푸시풀/등반 업데이트
            _actionClimb.Update();
            _actionPushPull.Update();

            // 5) 전투/피격 등 제약 상태면 이동 처리 제한
            if (_characterBase.IsStatusAttack()) return;
            if (_characterBase.IsStatusAttackComboWait()) return;
            if (_characterBase.IsStatusDamage()) return;
            if (_characterBase.IsStatusClimb()) return;
            if (_characterBase.IsStatusPush()) return;
            if (_characterBase.IsStatusSimulationTool()) return;

            // 6) 점프/낙하 중 이동 처리
            if (_characterBase.IsStatusJump())
            {
                if (move != Vector2.zero)
                {
                    OnJumpMoveContinuous(move);
                }
                return;
            }

            // 7) 대시 중 이동 처리
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

            // 8) 지상 이동/정지 처리
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
            if (_characterBase.IsStatusDead()) return;
            // 방향키 누르고 있는 동안 계속 호출됨
            // Debug.Log($"Moving: {direction}");
            _actionMove.JumpMove(direction);
        }
        private void OnMoveContinuous(Vector2 direction)
        {
            if (_characterBase.IsStatusDead()) return;
            // 방향키 누르고 있는 동안 계속 호출됨
            // Debug.Log($"Moving: {direction}");
            _actionMove.Move(direction);
        }
        public void OnAttack(InputAction.CallbackContext ctx)
        {
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Attack, Vector2.zero)) return;
            _attackHandler?.Handle(ctx);
        }
        public void OnJump(InputAction.CallbackContext ctx)
        {
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Jump, Vector2.zero)) return;
            _jumpHandler?.Handle(ctx);
        }
        public void OnDash(InputAction.CallbackContext ctx)
        {
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Dash, Vector2.zero)) return;
            _dashHandler?.Handle(ctx);
        }
        /// <summary>
        /// F 입력 처리: 가장 우선순위 높은 상호작용 대상 선택 → Begin/End 토글
        /// </summary>
        private void OnInteraction(InputAction.CallbackContext ctx)
        {
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Interaction, Vector2.zero)) return;

            _interactionHandler?.Handle(
                ctx,
                gameObject,
                _characterBase,
                () => _actionWall is { IsWallLocked: true },
                () =>
                {
                    // 0) 대시/점프/공격 중 상호작용 제한
                    if (_characterBase.IsStatusDash() || _characterBase.IsStatusAttack()) return true;
                    // 스킬 사용 중 상호작용 제한
                    if (_characterBase.IsStatusCastingSkill() || _characterBase.IsStatusUseSkill()) return true;
                    return false;
                });
        }
        /// <summary>
        /// 시뮬레이션 툴 사용
        /// </summary>
        private void OnSimulationTool(InputAction.CallbackContext ctx)
        {
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.SimulationTool, Vector2.zero)) return;
            _simulationToolHandler?.OnSimulationTool(ctx);
        }

        // === ActionLadder/PushPull 과의 연결 API ===

        public bool TryBeginLadder(ObjectClimb climb)
        {
            if (_characterBase.IsStatusDead()) return false;
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
            _interactionHandler?.ClearIfSame(climb); // 안전망
        }

        public bool TryBeginPushPull(ObjectPushPull target)
        {
            if (_characterBase.IsStatusDead()) return false;
            if (_actionDash.IsDashing) _actionDash.CancelDash(true);
            // 점프 중에는 밀기/당기기 금지(필요 시 조건 수정)
            if (_characterBase.IsStatusJump()) return false;

            return _actionPushPull.Begin(target);
        }

        public void EndPushPull(ObjectPushPull target)
        {
            _actionPushPull.End(target);
            _interactionHandler?.ClearIfSame(target); // 안전망
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
            _interactionHandler?.ClearIfEnded(ended);
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

            _toolAction?.Cancel(); //  툴 지속 상태 강제 종료

            _actionWall?.CancelWall(restorePrevious: true);

        }
        public void SetToolAction(IToolAction toolAction)
        {
            // 기존 액션 정리
            if (_toolAction != null)
            {
                _toolAction.OnDestroy();
                _toolAction = null;
            }

            _toolAction = toolAction;

            // 핸들러에도 주입
            _simulationToolHandler?.SetToolAction(_toolAction);

            if (_toolAction != null)
                _toolAction.Initialize(this, _characterBase, _characterBaseController);
        }
    }
}