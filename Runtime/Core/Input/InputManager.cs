using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// Player Input Asset에 등록한 키보드, 마우스, 게임 패드등의 입력 처리
    /// Player 에 AddComponent 된다.
    /// </summary>
    public class InputManager : MonoBehaviour, IAutoMoveMovementDriver, IIncomingHitGuardResolver
    {
        /// <summary>
        /// Control 패키지의 InputManager가 실제 이동 실행(Run/Move)을 담당합니다.
        /// (AutoMove가 활성화되어도 PlayerAutoMoveController가 Run()을 중복 호출하지 않도록 합니다.)
        /// </summary>
        public bool DrivesAutoMoveMovement => true;

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
        
        // 방어 처리
        private ActionGuard _actionGuard;
        private StaminaRegenController _staminaRegen;

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
        private GuardInputHandler _guardHandler;
        private JumpInputHandler _jumpHandler;
        private DashInputHandler _dashHandler;

        // === 추가 필드 ===
        private InteractionScanner2D _scanner;
        private InteractionInputHandler _interactionHandler;
        private GGemCoPlayerActionSettings _playerActionSettings;

        // Simulation Tool: UI 위 클릭 방지(다음 프레임에서 판정)
        private SimulationToolInputHandler _simulationToolHandler;

        // === Release 기반 입력 버퍼(Press → Release 정규화) ===
        private BufferedReleaseResolver _releaseResolver;
        private InputAction.CallbackContext _simulationToolPressCtx;
        private InputAction.CallbackContext _simulationToolReleaseCtx;

        // AutoMove(Core)
        private AutoMoveAdapter _autoMove;

        // 플레이어 공격 영역에 몬스터 진입 상태
        private PlayerAttackAreaState _attackAreaState;

        private void Awake()
        {
            _characterBase = GetComponent<CharacterBase>();
            if (!_characterBase)
            {
                enabled = false;
                return;
            }

            // 기본값(80ms)으로 초기화. Settings가 로드되면 ApplySettings에서 갱신됩니다.
            _releaseResolver = new BufferedReleaseResolver(0.08f);

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

            // 패트롤 영역 진입 상태 캐시(없어도 동작해야 함)
            _attackAreaState = player != null ? player.GetComponent<PlayerAttackAreaState>() : null;

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

            // === Release 기반 입력 버퍼 ===
            float waitMs = _playerActionSettings != null ? _playerActionSettings.pressToReleaseMaxWaitMs : 80f;
            float waitSec = waitMs * 0.001f;
            if (_releaseResolver == null) _releaseResolver = new BufferedReleaseResolver(waitSec);
            else _releaseResolver.SetWaitWindowSeconds(waitSec);

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

            // 스테미나 회복 설정 스냅샷 갱신
            _staminaRegen?.ApplySettings(_playerActionSettings);
        }

        private void InitializeControls()
        {
            _actionAttack = new ActionAttack();
            _actionAttack.Initialize(this, _characterBase, _characterBaseController);
            
            _actionGuard = new ActionGuard();
            _actionGuard.Initialize(this, _characterBase, _characterBaseController);

            // "가드가 아닐 때" 스테미나 회복 정책
            _staminaRegen = new StaminaRegenController(_characterBase, _actionGuard);
            _staminaRegen.ApplySettings(_playerActionSettings);

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
                var wallDebug = ControlPackageManager.Instance.gameObject.GetComponent<ActionWallDebugDrawer>();
                if (wallDebug == null)
                {
                    wallDebug = ControlPackageManager.Instance.gameObject.AddComponent<ActionWallDebugDrawer>();
                }

                wallDebug.Bind(_actionWall, _playerActionSettings);
            }
#endif
            // 벽 액션 진행 여부를 점프에 전달(클리프 폴/착지 전이 충돌 방지)
            _actionJump.SetWallActionActiveQuery(() =>
                _actionWall != null && (_actionWall.IsWallLocked || _actionWall.IsKinematicWallJumping));

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
            _guardHandler = new GuardInputHandler(_characterBase, _actionGuard, _policy);
            _jumpHandler = new JumpInputHandler(_characterBase, _actionJump, _actionWall, _policy);
            _dashHandler = new DashInputHandler(_characterBase, _actionDash, _policy);

            // === Release 기반 입력 버퍼 ===
            if (_releaseResolver != null)
            {
                _releaseResolver.Resolved -= OnResolvedChord;
                _releaseResolver.Resolved += OnResolvedChord;
            }
        }

        private void InitializeInputPlayer()
        {
            // PlayerInput이 루트가 아닌 자식에 배치된 프로젝트 구성도 흔하므로,
            // 우선 루트에서 찾고 없으면 자식에서 검색합니다(비활성 포함).
            _playerInput = GetComponent<PlayerInput>();
            if (!_playerInput)
            {
                _playerInput = GetComponentInChildren<PlayerInput>(true);
            }
            if (!_playerInput) return;
            _bindings = new PlayerInputBindings();
            _bindings.Bind(
                _playerInput,
                OnAttackPress,
                OnAttackRelease,
                OnGuardPress,
                OnGuardRelease,
                OnJumpPress,
                OnJumpRelease,
                OnDashPress,
                OnDashRelease,
                OnInteractionPress,
                OnInteractionRelease,
                OnSimulationToolPress,
                OnSimulationToolRelease);

            if (_playerInput != null)
                _playerInput.onControlsChanged += OnChangeControlScheme;
        }

        private void OnDestroy()
        {
            _actionAttack?.OnDestroy();
            _actionGuard?.OnDestroy();
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

            if (_releaseResolver != null)
            {
                _releaseResolver.Resolved -= OnResolvedChord;
                _releaseResolver = null;
            }

            if (_releaseResolver != null)
            {
                _releaseResolver.Resolved -= OnResolvedChord;
                _releaseResolver = null;
            }

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

            // 80ms 입력 버퍼 마감 처리(가상 릴리즈)
            _releaseResolver?.Tick(Time.unscaledTime);

            // Guard 유지(스테미나 틱/자동 해제) 처리
            _actionGuard?.Tick(Time.deltaTime);

            // 스테미나 회복(가드 중이 아닐 때)
            _staminaRegen?.Tick(Time.deltaTime);
        }

        public bool TryResolveIncomingHit(MetadataDamage metadataDamage, out GuardResolutionResult result)
        {
            result = default;
            if (_actionGuard == null) return false;
            return _actionGuard.TryResolveIncomingHit(metadataDamage, out result);
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
        private void UpdateAutoMoveSuspendByGuard()
        {
            if (_autoMove == null || _actionGuard == null) return;
            bool guardActive = _actionGuard.IsGuarding;
            _autoMove.TickSuspendByGuard(guardActive);
        }

        private void UpdateAutoMoveSuspendByControlLocked()
        {
            if (_autoMove == null || _characterBase == null) return;

            bool locked = _characterBase.IsStatusDontControl() || _characterBase.IsStatusDead();
            _autoMove.TickSuspendByControlLocked(locked);
        }

        private void UpdateAutoMoveSuspendByPatrolArea()
        {
            if (_autoMove == null) return;

            // 몬스터 패트롤 영역(ObjectPatrol)에 진입하면 AutoMove를 일시 정지한다.
            bool active = _attackAreaState != null && _attackAreaState.IsInAttackArea;
            _autoMove.TickSuspendByPlayerAttackRange(active);
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
            UpdateAutoMoveSuspendByGuard();
            UpdateAutoMoveSuspendByPatrolArea();

            UpdateAutoMoveSuspendByControlLocked();


            // DontControl(그로기/컷씬 등) 중에는 입력/자동 이동을 포함한 제어 로직을 중지한다.
            // - 물리/착지 전이는 유지하기 위해 Jump/Dash Update는 수행한다.
            if (_characterBase.IsStatusDontControl())
            {
                _actionJump.Update();
                _actionDash.Update();

                // 진행 중인 특수 이동은 즉시 종료
                if (_actionDash != null && _actionDash.IsDashing)
                    _actionDash.CancelDash(skipEndAnimation: true);

                // 벽 고정/키네마틱 점프 등 특수 Wall 상태도 해제
                _actionWall?.CancelWall(restorePrevious: false);

                // 이동 입력/자동 이동 모두 중지
                // _characterBase.Stop();
                return;
            }

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

            if (_actionGuard is { IsGuarding: true })
            {
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

        // === Press/Release 수집(실행은 ReleaseResolver에서 수행) ===
        // Attack
        private void OnAttackPress(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsStatusDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Attack, Vector2.zero)) return;
            _releaseResolver?.PushPress(PlayerButtonId.Attack, Time.unscaledTime);
        }

        private void OnAttackRelease(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsStatusDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Attack, Vector2.zero)) return;
            _releaseResolver?.PushRelease(PlayerButtonId.Attack, Time.unscaledTime);
        }
        
        // Guard
        private void OnGuardPress(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsStatusDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Guard, Vector2.zero)) return;

            // Guard는 "홀드" 입력이므로 릴리즈 버퍼(Chord) 시스템을 통하지 않고 즉시 시작합니다.
            // - started: 버튼 Down
            // - canceled: 버튼 Up
            _guardHandler?.HandlePress();
        }

        private void OnGuardRelease(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsStatusDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Guard, Vector2.zero)) return;
            _guardHandler?.HandleRelease();
        }

        // Jump
        private void OnJumpPress(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsStatusDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Jump, Vector2.zero)) return;
            _releaseResolver?.PushPress(PlayerButtonId.Jump, Time.unscaledTime);
        }

        private void OnJumpRelease(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsStatusDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Jump, Vector2.zero)) return;
            _releaseResolver?.PushRelease(PlayerButtonId.Jump, Time.unscaledTime);
        }
        
        // Dash
        private void OnDashPress(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsStatusDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Dash, Vector2.zero)) return;
            _releaseResolver?.PushPress(PlayerButtonId.Dash, Time.unscaledTime);
        }

        private void OnDashRelease(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsStatusDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Dash, Vector2.zero)) return;
            _releaseResolver?.PushRelease(PlayerButtonId.Dash, Time.unscaledTime);
        }

        /// <summary>
        /// F 입력 처리: 가장 우선순위 높은 상호작용 대상 선택 → Begin/End 토글
        /// </summary>
        private void OnInteractionPress(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsStatusDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Interaction, Vector2.zero)) return;
            _releaseResolver?.PushPress(PlayerButtonId.Interaction, Time.unscaledTime);
        }

        private void OnInteractionRelease(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsStatusDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Interaction, Vector2.zero)) return;
            _releaseResolver?.PushRelease(PlayerButtonId.Interaction, Time.unscaledTime);
        }

        /// <summary>
        /// 시뮬레이션 툴 사용
        /// </summary>
        private void OnSimulationToolPress(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsStatusDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.SimulationTool, Vector2.zero)) return;
            _simulationToolPressCtx = ctx;
            _releaseResolver?.PushPress(PlayerButtonId.SimulationTool, Time.unscaledTime);
        }

        private void OnSimulationToolRelease(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsStatusDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.SimulationTool, Vector2.zero)) return;
            _simulationToolReleaseCtx = ctx;
            _releaseResolver?.PushRelease(PlayerButtonId.SimulationTool, Time.unscaledTime);
        }

        // === Chord 확정(릴리즈) 처리 ===
        private void OnResolvedChord(ResolvedButtonChord chord)
        {
            // 0) Interaction은 토글 동작이라 동시입력에서도 우선 처리(예시 정책)
            //    - 후보가 없을 때 다른 액션까지 수행하고 싶다면, 아래 3)에서 설명하는 "bool 반환" 방식으로 개선 권장
            if (chord.Buttons.Contains(PlayerButtonId.Interaction))
            {
                _interactionHandler?.Handle(
                    gameObject,
                    _characterBase,
                    () => _actionWall is { IsWallLocked: true },
                    () =>
                    {
                        if (_characterBase.IsStatusDash() || _characterBase.IsStatusAttack()) return true;
                        if (_characterBase.IsStatusCastingSkill() || _characterBase.IsStatusUseSkill()) return true;
                        return false;
                    });
                return;
            }

            // 1) 조합(Chord) 우선 처리: 소비하면 return
            if (TryHandleChordCombo(in chord))
                return;

            // 2) 조합이 없으면 기존 기본 동작(fallback): 포함된 버튼을 정해진 순서로 모두 실행
            DispatchSingles(in chord);
        }

        /// <summary>
        /// 동시 입력 처리
        /// </summary>
        /// <param name="chord"></param>
        /// <returns></returns>
        private bool TryHandleChordCombo(in ResolvedButtonChord chord)
        {
            // PlayerButtonSet은 순서 무관 비트마스크입니다.
            // 정확한 조합만 잡고 싶으면 == 비교가 가장 명확합니다.
            var set = chord.Buttons;

            // 예시 1) Attack + Jump
            // - 지상: Jump 우선(점프), 공중: Attack 우선(공중 공격)
            PlayerButtonSet attackJump = PlayerButtonSet
                .From(PlayerButtonId.Attack)
                .Add(PlayerButtonId.Jump);

            if (set == attackJump)
            {
                // _actionJump.IsGroundedByCollision()는 기존 코드에서 사용 중인 지상 판정 API입니다.
                bool grounded = _actionJump != null && _actionJump.IsGroundedByCollision();

                if (grounded)
                {
                    _jumpHandler?.Handle();
                }
                else
                {
                    _attackHandler?.Handle();
                }

                return true; // 소비
            }

            // 예시 2) Attack + Dash = 대시 어택(예시 정책)
            PlayerButtonSet attackDash = PlayerButtonSet
                .From(PlayerButtonId.Attack)
                .Add(PlayerButtonId.Dash);

            if (set == attackDash)
            {
                // “대시로 진입 → 공격” 느낌
                _dashHandler?.Handle();
                _attackHandler?.Handle();
                return true;
            }

            // 예시 3) Jump + Dash = 회피 점프(예시 정책)
            PlayerButtonSet jumpDash = PlayerButtonSet
                .From(PlayerButtonId.Jump)
                .Add(PlayerButtonId.Dash);

            if (set == jumpDash)
            {
                GcLogger.Log($"chord: {set}");
                // _jumpHandler?.Handle();
                // _dashHandler?.Handle();
                return true;
            }

            // 예시 4) SimulationTool + Attack 처럼 조합을 잡고 싶을 때
            // - SimulationTool은 ctx가 필요하므로 chord.IsVirtual(...)을 함께 사용
            PlayerButtonSet toolAttack = PlayerButtonSet
                .From(PlayerButtonId.SimulationTool)
                .Add(PlayerButtonId.Attack);

            if (set == toolAttack)
            {
                // Tool 먼저 실행(또는 반대)
                var ctx = chord.IsVirtual(PlayerButtonId.SimulationTool)
                    ? _simulationToolPressCtx
                    : _simulationToolReleaseCtx;
                _simulationToolHandler?.HandleResolved(ctx);

                _attackHandler?.Handle();
                return true;
            }

            return false; // 조합 미처리 → fallback으로
        }

        private void DispatchSingles(in ResolvedButtonChord chord)
        {
            // 기본 동작: 동시 입력이면 정해진 순서로 모두 실행
            if (chord.Buttons.Contains(PlayerButtonId.Attack))
            {
                _attackHandler?.Handle();
            }
            
            if (chord.Buttons.Contains(PlayerButtonId.Guard))
            {
                _guardHandler?.Handle();
            }

            if (chord.Buttons.Contains(PlayerButtonId.Jump))
            {
                _jumpHandler?.Handle();
            }

            if (chord.Buttons.Contains(PlayerButtonId.Dash))
            {
                _dashHandler?.Handle();
            }

            if (chord.Buttons.Contains(PlayerButtonId.SimulationTool))
            {
                var ctx = chord.IsVirtual(PlayerButtonId.SimulationTool)
                    ? _simulationToolPressCtx
                    : _simulationToolReleaseCtx;
                _simulationToolHandler?.HandleResolved(ctx);
            }
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
            _actionGuard?.CancelGuard(true);

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