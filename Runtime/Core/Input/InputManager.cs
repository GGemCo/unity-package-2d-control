using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// Player Input Asset에 등록한 키보드, 마우스, 게임 패드등의 입력 처리
    /// Player 에 AddComponent 된다.
    /// </summary>
    public class InputManager : MonoBehaviour, IAutoMoveMovementDriver, IIncomingHitGuardResolver, IIncomingHitActionCanceler, ISkillStartActionCanceler, IMapClearActionCanceler, IInteractionActionCanceler, IPlayerExhaustionStateSource, ICameraVerticalFollowStateSource
    {
        /// <summary>
        /// Control 패키지의 InputManager가 실제 이동 실행(Run/Move)을 담당합니다.
        /// (AutoMove가 활성화되어도 PlayerAutoMoveController가 Run()을 중복 호출하지 않도록 합니다.)
        /// </summary>
        public bool DrivesAutoMoveMovement => true;

        /// <summary>
        /// 현재 탈진 상태 여부입니다.
        /// </summary>
        public bool IsExhausting => _exhaustion?.IsExhausting ?? false;

        /// <summary>
        /// 점프 중 카메라의 세로 추적 영향도를 낮춰야 하는 상태인지 반환합니다. 항상 적용하기위해 true 로 설정
        /// </summary>
        public bool IsVerticalFollowInfluenceActive => true;

        /// <summary>
        /// 탈진 상태가 시작/종료될 때 외부 시스템(UI 등)에 전달합니다.
        /// </summary>
        public event System.Action<bool> ExhaustionStateChanged;

        private CharacterBase _characterBase;
        private CharacterBaseController _characterBaseController;
        private ICharacterMotionController _motionController;

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
        private PlayerExhaustionController _exhaustion;

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
        private GGemCoPlayerGuardSettings _playerGuardSettings;

        // Simulation Tool: UI 위 클릭 방지(다음 프레임에서 판정)
        private SimulationToolInputHandler _simulationToolHandler;

        // === Release 기반 입력 버퍼(Press → Release 정규화) ===
        private BufferedReleaseResolver _releaseResolver;
        private InputAction.CallbackContext _simulationToolPressCtx;
        private InputAction.CallbackContext _simulationToolReleaseCtx;

        // AutoMove(Core)
        private AutoMoveAdapter _autoMove;
        private IAutoMoveVectorProvider _autoMoveProvider;
        private MapManager _mapManagerForLoadEvents;

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
            _playerGuardSettings = AddressableLoaderSettingsControl.Instance.playerGuardSettings;
            if (_playerActionSettings)
            {
                ApplySettings();
#if UNITY_EDITOR
                // 플레이 중 인스펙터 수정 → 즉시 반영
                _playerActionSettings.Changed += ApplySettings;
#endif
            }
            if (_playerGuardSettings)
            {
                ApplyGuardSettings();
#if UNITY_EDITOR
                // 플레이 중 인스펙터 수정 → 즉시 반영
                _playerGuardSettings.Changed += ApplyGuardSettings;
#endif
            }

            _characterBaseController = GetComponent<CharacterBaseController>();
            _motionController = GetComponent<ICharacterMotionController>();

            // AutoMove: 이동 벡터 오버라이드/입력 잠금 + Suspend 관리
            _autoMoveProvider = GetComponent<IAutoMoveVectorProvider>();
            _autoMove = new AutoMoveAdapter(
                _autoMoveProvider,
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

        /// <summary>
        /// 컴포넌트가 활성화될 때 맵 로드 시작 이벤트를 구독합니다.
        /// 맵 전환 직전에 AutoMove 잠금 토큰을 소유자 경로로 정리하기 위한 진입점입니다.
        /// </summary>
        private void OnEnable()
        {
            SubscribeMapLoadStartEventIfNeeded();
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

        }

        /// <summary>
        /// 플레이어 가드 설정 변경 사항을 스테미나 회복/탈진 컨트롤러에 반영합니다.
        /// </summary>
        private void ApplyGuardSettings()
        {
            // 스테미나 회복 설정 스냅샷 갱신
            _staminaRegen?.ApplySettings(_playerGuardSettings);
            _exhaustion?.ApplySettings(_playerGuardSettings);
        }

        private void InitializeControls()
        {
            _actionAttack = new ActionAttack();
            _actionAttack.Initialize(this, _characterBase, _characterBaseController);
            
            _actionGuard = new ActionGuard();
            _actionGuard.Initialize(this, _characterBase, _characterBaseController);

            // "가드가 아닐 때" 스테미나 회복 정책
            _staminaRegen = new StaminaRegenController(_characterBase, _actionGuard);
            _staminaRegen.ApplySettings(_playerGuardSettings);

            _exhaustion = new PlayerExhaustionController(_characterBase, _actionGuard, CancelActionsForExhaustion);
            _exhaustion.StateChanged += OnExhaustionStateChanged;
            _exhaustion.ApplySettings(_playerGuardSettings);

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
            // 점프 접지/천장 판정 Gizmo 렌더링 프록시 바인딩
            if (_playerActionSettings.EnableJumpProbeDebugGizmos)
            {
                var jumpDebug = _characterBase.gameObject.GetComponent<ActionJumpDebugDrawer>();
                if (jumpDebug == null)
                {
                    jumpDebug = _characterBase.gameObject.AddComponent<ActionJumpDebugDrawer>();
                }

                jumpDebug.Bind(_actionJump, _playerActionSettings);
            }

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
            ApplyGuardSettings();

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
            UnsubscribeMapLoadStartEvent();

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

            if (_exhaustion != null)
            {
                _exhaustion.StateChanged -= OnExhaustionStateChanged;
                _exhaustion.Dispose();
                _exhaustion = null;
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

            if (_playerGuardSettings)
            {
#if UNITY_EDITOR
                // 플레이 중 인스펙터 수정 → 즉시 반영
                _playerGuardSettings.Changed -= ApplyGuardSettings;
#endif
            }

            Player player = _characterBase as Player;
            player?.onEventDeadByEndGround.RemoveAllListeners();
        }

        /// <summary>
        /// SceneGame.MapManager의 맵 로드 시작 이벤트를 안전하게 구독합니다.
        /// 이미 구독 중이거나 SceneGame 초기화 전이면 구독을 건너뜁니다.
        /// </summary>
        private void SubscribeMapLoadStartEventIfNeeded()
        {
            if (_mapManagerForLoadEvents != null)
            {
                return;
            }

            SceneGame sceneGame = SceneGame.Instance;
            if (sceneGame == null || sceneGame.mapManager == null)
            {
                return;
            }

            _mapManagerForLoadEvents = sceneGame.mapManager;
            _mapManagerForLoadEvents.OnLoadStartMap += OnMapLoadStart;
        }

        /// <summary>
        /// 맵 로드 시작 이벤트 구독을 해제합니다.
        /// </summary>
        private void UnsubscribeMapLoadStartEvent()
        {
            if (_mapManagerForLoadEvents == null)
            {
                return;
            }

            _mapManagerForLoadEvents.OnLoadStartMap -= OnMapLoadStart;
            _mapManagerForLoadEvents = null;
        }

        /// <summary>
        /// 맵 로드가 시작되면 AutoMove 관련 런타임 상태를 정리합니다.
        /// - Suspend 토큰은 소유자(<see cref="AutoMoveAdapter"/>) 경로로 해제합니다.
        /// - 진행 중 오토워크는 취소해 다음 맵 로드 시 이전 요청이 이어지지 않도록 합니다.
        /// </summary>
        private void OnMapLoadStart()
        {
            CleanupAutoMoveStateOnMapLoadStart();
        }

        /// <summary>
        /// 맵 전환 시작 시 AutoMove 잠금 토큰과 이동 요청 상태를 안전하게 정리합니다.
        /// </summary>
        private void CleanupAutoMoveStateOnMapLoadStart()
        {
            _autoMove?.ReleaseAll();

            if (_autoMoveProvider is PlayerAutoMoveController autoMoveController)
            {
                autoMoveController.Cancel();
            }
        }

        private void Update()
        {
            if (_mapManagerForLoadEvents == null)
            {
                SubscribeMapLoadStartEventIfNeeded();
            }

            if (_characterBase != null && _characterBase.IsHitStopped)
            {
                return;
            }

            _simulationToolHandler?.Tick();

            // 80ms 입력 버퍼 마감 처리(가상 릴리즈)
            _releaseResolver?.Tick(Time.unscaledTime);

            // Guard 유지(스테미나 틱/자동 해제) 처리
            _actionGuard?.Tick(Time.deltaTime);

            // 탈진 처리(스테미나 0 진입/전용 회복/애니메이션)
            _exhaustion?.Tick(Time.deltaTime);

            // 일반 스테미나 회복은 탈진 중에는 중단합니다.
            if (!(_exhaustion?.IsExhausting ?? false))
            {
                _staminaRegen?.Tick(Time.deltaTime);
            }
        }

        public bool TryResolveIncomingHit(MetadataDamage metadataDamage, out GuardResolutionResult result)
        {
            result = default;
            if (_actionGuard == null) return false;
            return _actionGuard.TryResolveIncomingHit(metadataDamage, out result);
        }

        /// <summary>
        /// 플레이어 스킬 시작 직전에 점프/대시 등 잔존 중인 입력 액션을 정리합니다.
        /// Ground Slam 같은 공중 스킬이 시작된 뒤에도 이전 Jump FSM이 살아남아 Fall 애니메이션을 다시 점유하는 문제를 방지합니다.
        /// </summary>
        public void CancelActionsOnSkillStart()
        {
            _releaseResolver?.Clear();

            _actionJump?.CancelJump(skipLandAnimation: true, restoreGravity: true);
            _actionDash?.CancelDash(skipEndAnimation: true);
            _actionClimb?.CancelClimb(skipEndAnimation: true, restoreGravity: true);
            _actionPushPull?.Cancel();
            _toolAction?.Cancel();
            _actionWall?.CancelWall(restorePrevious: false);
            _actionGuard?.CancelGuard(true);

            _autoMove?.ReleaseAll();
        }

        /// <summary>
        /// 맵 클리어 종료 정책이 확정되었을 때 진행 중인 플레이어 조작 상태를 정리합니다.
        /// 월드맵 UI가 열리기 전 자동 이동 요청을 완전히 취소하고, 점프/대시/가드/상호작용 등 잔여 액션이 다음 화면으로 이어지지 않게 합니다.
        /// </summary>
        public void CancelActionsOnMapClear()
        {
            _releaseResolver?.Clear();
            _simulationToolPressCtx = default;
            _simulationToolReleaseCtx = default;

            _actionJump?.CancelJump(skipLandAnimation: true, restoreGravity: true);
            _actionDash?.CancelDash(skipEndAnimation: true);
            _actionClimb?.CancelClimb(skipEndAnimation: true, restoreGravity: true);
            _actionPushPull?.Cancel();
            _toolAction?.Cancel();
            _actionWall?.CancelWall(restorePrevious: false);
            _actionGuard?.CancelGuard(true);

            _autoMove?.ReleaseAll();

            if (_autoMoveProvider is PlayerAutoMoveController autoMoveController)
            {
                autoMoveController.Cancel();
            }

            if (_characterBase != null && !_characterBase.IsStatusDead())
            {
                _characterBase.Stop();
            }
        }

        /// <summary>
        /// NPC 인터랙션 시작 직전에 진행 중인 플레이어 조작 상태를 정리합니다.
        /// 대화 중에는 UI 터치만 처리되어야 하므로 입력 버퍼, 이동계 액션, 가드, 자동 이동 요청을 모두 종료합니다.
        /// </summary>
        public void CancelActionsOnInteractionStart()
        {
            _releaseResolver?.Clear();
            _simulationToolPressCtx = default;
            _simulationToolReleaseCtx = default;

            _actionJump?.CancelJump(skipLandAnimation: true, restoreGravity: true);
            _actionDash?.CancelDash(skipEndAnimation: true);
            _actionClimb?.CancelClimb(skipEndAnimation: true, restoreGravity: true);
            _actionPushPull?.Cancel();
            _toolAction?.Cancel();
            _actionWall?.CancelWall(restorePrevious: false);
            _actionGuard?.CancelGuard(true);

            _autoMove?.ReleaseAll();

            if (_autoMoveProvider is PlayerAutoMoveController autoMoveController)
            {
                autoMoveController.Cancel();
            }

            if (_characterBase != null && !_characterBase.IsStatusDead())
            {
                _characterBase.Stop();
            }
        }

        /// <summary>
        /// 피격/사망으로 인해 진행 중인 입력 액션을 강제 종료합니다.
        /// 가드 상태가 남아서 다음 입력이 막히는 문제를 방지하기 위해, 상태 전환 전에 관련 액션과 입력 버퍼를 함께 정리합니다.
        /// </summary>
        /// <param name="reason">액션 취소 사유입니다.</param>
        public void CancelActionsOnIncomingHit(IncomingHitCancelReason reason)
        {
            _releaseResolver?.Clear();

            _actionJump?.CancelJump(skipLandAnimation: true, restoreGravity: true);
            _actionDash?.CancelDash(skipEndAnimation: true);
            _actionClimb?.CancelClimb(skipEndAnimation: true, restoreGravity: true);
            _actionPushPull?.Cancel();
            _toolAction?.Cancel();
            _actionWall?.CancelWall(restorePrevious: reason == IncomingHitCancelReason.Death);
            _actionGuard?.CancelGuard(true);

            _autoMove?.ReleaseAll();

            if (reason == IncomingHitCancelReason.Death)
            {
                _simulationToolPressCtx = default;
                _simulationToolReleaseCtx = default;
            }
        }

        private void OnExhaustionStateChanged(bool isExhausting)
        {
            ExhaustionStateChanged?.Invoke(isExhausting);
        }

        private void CancelActionsForExhaustion()
        {
            _releaseResolver?.Clear();
            _simulationToolPressCtx = default;
            _simulationToolReleaseCtx = default;

            _actionJump?.CancelJump(skipLandAnimation: true, restoreGravity: true);
            _actionDash?.CancelDash(skipEndAnimation: true);
            _actionClimb?.CancelClimb(skipEndAnimation: true, restoreGravity: true);
            _actionPushPull?.Cancel();
            _toolAction?.Cancel();
            _actionWall?.CancelWall(restorePrevious: false);
            _actionGuard?.CancelGuard(true);

            _autoMove?.ReleaseAll();
        }

        private void OnDisable()
        {
            // Wall Action 등에서 Suspend를 쥔 상태로 비활성화될 수 있으므로, 누락 없이 해제한다.
            _autoMove?.ReleaseAll();
            UnsubscribeMapLoadStartEvent();
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

            bool locked = _characterBase.IsDontControl() || _characterBase.IsStatusDead();
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
            if (_characterBase.IsHitStopped) return;

            // todo. 정리 필요
            if (_characterBase.IsStatusCastingSkill()) return;
            if (_characterBase.IsStatusUseSkill()) return;

            // Wall Action 진행 중에는 AutoMove를 Pause 한다.
            UpdateAutoMoveSuspendByWall();
            UpdateAutoMoveSuspendByGuard();
            UpdateAutoMoveSuspendByPatrolArea();

            UpdateAutoMoveSuspendByControlLocked();


            // DontControl(그로기/컷씬 등) 중에는 입력/자동 이동을 포함한 제어 로직을 중지한다.
            // - CrowdControl 모션 재생 중에는 Jump가 새 상태를 획득하지 못하도록 passive fall 감지를 막는다.
            // - 이미 활성화된 Jump FSM만 필요 시 유지하여, 외부 상태(DontControl)를 덮어쓰지 않도록 한다.
            if (_characterBase.IsDontControl())
            {
                bool isCrowdControlMotionPlaying =
                    _motionController != null && _motionController.IsPlaying(MotionChannel.CrowdControl);

                if (!isCrowdControlMotionPlaying)
                    _actionJump.TickActiveFsmOnly(suppressStatusRelease: true);

                _actionDash.Update();

                // 진행 중인 특수 이동은 즉시 종료
                if (_actionDash != null && _actionDash.IsDashing)
                    _actionDash.CancelDash(skipEndAnimation: true);

                // 벽 고정/키네마틱 점프 등 특수 Wall 상태도 해제
                _actionWall?.CancelWall(restorePrevious: false);

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
            if (_characterBase != null && _characterBase.IsDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Attack, Vector2.zero)) return;
            _releaseResolver?.PushPress(PlayerButtonId.Attack, Time.unscaledTime);
        }

        private void OnAttackRelease(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Attack, Vector2.zero)) return;
            _releaseResolver?.PushRelease(PlayerButtonId.Attack, Time.unscaledTime);
        }
        
        // Guard
        private void OnGuardPress(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Guard, Vector2.zero)) return;

            // Guard는 "홀드" 입력이므로 릴리즈 버퍼(Chord) 시스템을 통하지 않고 즉시 시작합니다.
            // - started: 버튼 Down
            // - canceled: 버튼 Up
            _guardHandler?.HandlePress();
        }

        /// <summary>
        /// Guard 버튼 Release 입력을 처리합니다.
        /// Release는 새 행동 시작이 아니라 진행 중인 가드 상태를 정리하는 입력이므로,
        /// CC/DontControl 상태에서도 <see cref="ActionGuard"/>까지 전달합니다.
        /// </summary>
        /// <param name="ctx">Input System에서 전달된 입력 콜백 컨텍스트입니다.</param>
        private void OnGuardRelease(InputAction.CallbackContext ctx)
        {
            _guardHandler?.HandleRelease();
        }

        // Jump
        private void OnJumpPress(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Jump, Vector2.zero)) return;
            _releaseResolver?.PushPress(PlayerButtonId.Jump, Time.unscaledTime);
        }

        private void OnJumpRelease(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Jump, Vector2.zero)) return;
            _releaseResolver?.PushRelease(PlayerButtonId.Jump, Time.unscaledTime);
        }
        
        // Dash
        private void OnDashPress(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Dash, Vector2.zero)) return;
            _releaseResolver?.PushPress(PlayerButtonId.Dash, Time.unscaledTime);
        }

        private void OnDashRelease(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Dash, Vector2.zero)) return;
            _releaseResolver?.PushRelease(PlayerButtonId.Dash, Time.unscaledTime);
        }

        /// <summary>
        /// F 입력 처리: 가장 우선순위 높은 상호작용 대상 선택 → Begin/End 토글
        /// </summary>
        private void OnInteractionPress(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Interaction, Vector2.zero)) return;
            _releaseResolver?.PushPress(PlayerButtonId.Interaction, Time.unscaledTime);
        }

        private void OnInteractionRelease(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.Interaction, Vector2.zero)) return;
            _releaseResolver?.PushRelease(PlayerButtonId.Interaction, Time.unscaledTime);
        }

        /// <summary>
        /// 시뮬레이션 툴 사용
        /// </summary>
        private void OnSimulationToolPress(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.SimulationTool, Vector2.zero)) return;
            _simulationToolPressCtx = ctx;
            _releaseResolver?.PushPress(PlayerButtonId.SimulationTool, Time.unscaledTime);
        }

        private void OnSimulationToolRelease(InputAction.CallbackContext ctx)
        {
            if (_characterBase != null && _characterBase.IsDontControl()) return;
            if (_autoMove != null && _autoMove.ShouldBlockInput(AutoMoveInputType.SimulationTool, Vector2.zero)) return;
            _simulationToolReleaseCtx = ctx;
            _releaseResolver?.PushRelease(PlayerButtonId.SimulationTool, Time.unscaledTime);
        }

        // === Chord 확정(릴리즈) 처리 ===
        private void OnResolvedChord(ResolvedButtonChord chord)
        {
            if (_characterBase != null && _characterBase.IsDontControl())
                return;

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
            // 기존 옵션 패널의 스킴 표시는 유지 가능하며, 모바일 HUD도 현재 스킴 기준으로 즉시 갱신합니다.
            MobileInputHudService.Instance?.Refresh();
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
            CancelActionsOnIncomingHit(IncomingHitCancelReason.Death);
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
