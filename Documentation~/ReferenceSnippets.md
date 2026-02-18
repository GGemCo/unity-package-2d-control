# Control ReferenceSnippets

작성일: 2026-02-18

목적:
- Control 패키지에서 Action/입력/AutoMove 연동을 **ActionWall 스타일**로 자동 복제

우선순위:
1) `Docs/ReferenceSnippets.md`
2) `Docs/STYLE_CONTRACT.md`
3) `Docs/GOLDEN_REFERENCES.md`
4) `Docs/GGemCoPatterns/*`
5) Control `CONVENTIONS/ARCHITECTURE/PLAYBOOK`

---

## ActionWall(Phase 머신 중심 진입점)

- 경로: `Action/Wall/ActionWall.cs`
- 포인트:
  - Action은 비대해지면 안 됨: Phase로 분리
  - Enter/Tick/FixedTick 전환은 Switch에 위임

```csharp
    /// - Update(프레임): 입력 샘플링/타이머/상태 전이
    /// - FixedUpdate(고정): Rigidbody2D 이동/속도 적용
    /// </remarks>
    public partial class ActionWall : ActionBase
    {
        /// <summary>
        /// 벽 액션이 활성화되어 있는지 여부를 반환합니다.
        /// </summary>
        public bool IsEnabled => _enabled;

        /// <summary>
        /// 현재 Phase가 Hang/Slide 등 "벽에 고정(lock)"되는 상태인지 여부를 반환합니다.
        /// </summary>
        public bool IsWallLocked =>
            _current != null && (_current.Id == WallPhaseId.Hang || _current.Id == WallPhaseId.Slide);

        /// <summary>
        /// 현재 Phase가 벽 점프(Jump)이며, Kinematic 방식으로 점프를 시뮬레이션 중인지 여부를 반환합니다.
        /// </summary>
        public bool IsKinematicWallJumping => _current is { Id: WallPhaseId.Jump or WallPhaseId.JumpEnd };

        /// <summary>
        /// 현재 Phase가 벽 점프 종료(JumpEnd) 단계인지 여부를 반환합니다.
        /// </summary>
        public bool IsWallJumpEnding => _current is { Id: WallPhaseId.JumpEnd };

        /// <summary>
        /// 현재 Phase ID를 반환합니다. Phase가 없으면 <see cref="WallPhaseId.None"/>입니다.
        /// </summary>
        public WallPhaseId CurrentPhase => _current?.Id ?? WallPhaseId.None;

        /// <summary>
        /// 벽 액션 내부에서 누적되는 "입력 없음" 시간(초)입니다. (외부 디버깅/표시용)
        /// </summary>
        public float NoInputTimeSeconds { get; private set; }

        /// <summary>
        /// 외부(사망/강제 상태 전환 등)에서 벽 액션을 즉시 종료할 때 호출합니다.
        /// </summary>
        /// <param name="restorePrevious">
        /// true이면 벽 액션 진입 이전의 상태(이동/점프 등)를 복구하려고 시도합니다.
        /// </param>
        public void CancelWall(bool restorePrevious = true)
        {
            if (_current == null) return;
            ExitWall(forceRestorePrevious: restorePrevious);
        }

        /// <summary>
        /// 현재 붙어있는 벽 방향을 나타냅니다. (-1: 왼쪽 벽, +1: 오른쪽 벽)
        /// </summary>
        public int WallSideX { get; private set; }

        /// <summary>
        /// 벽 재부착 금지 쿨다운의 만료 시각(Time.time 기준)입니다.
        /// </summary>
        public float CooldownUntil => _cooldownUntil;

        /// <summary>
        /// 벽 액션에 필요한 컴포넌트(Rigidbody2D/Collider)와 센서를 준비하고 초기 상태를 캐시합니다.
        /// </summary>
        /// <param name="inputManager">입력 시스템 관리자입니다.</param>
        /// <param name="characterBase">캐릭터 데이터/컴포넌트 루트입니다.</param>
        /// <param name="characterBaseController">캐릭터 컨트롤러입니다.</param>
        public override void Initialize(
            InputManager inputManager,
            CharacterBase characterBase,
            CharacterBaseController characterBaseController)
        {
            _rigidbody = characterBase.characterRigidbody2D;
            _colliderMapObject = characterBase.colliderMapObject;

            if (_rigidbody == null || _colliderMapObject == null)
            {
                GcLogger.LogError("[ActionWall] Rigidbody2D/CapsuleCollider2D(colliderMapObject)가 필요합니다.");
                return;
            }

            // 벽 감지/접촉 판정을 담당하는 센서 초기화
            _sensor = new WallSensor2D(_colliderMapObject, _rigidbody);

            // 진입 전/후 복구를 위한 기존 모션/상태 캐시
            CacheMotionState();

            // 벽에 붙을 때 X축 앵커(고정 좌표) 관련 초기화
            _anchorX = float.NaN;
            _hasAnchorX = false;

            base.Initialize(inputManager, characterBase, characterBaseController);
        }

        /// <summary>
        /// InputManager.FixedUpdate에서 호출되어, 현재 Phase에 따라 벽 진입/유지/전이를 수행합니다.
        /// </summary>
        /// <param name="moveInput">현재 이동 입력 벡터입니다.</param>
        /// <remarks>
        /// - 벽 액션 "진입"은 공중(점프 상태)에서만 가능합니다.
        /// - Phase가 없으면 진입 조건을 평가하여 Hang으로 진입을 시도합니다.
        /// </remarks>
        public void FixedTick(Vector2 moveInput)
        {
            if (!_enabled) return;
            if (_rigidbody == null || _colliderMapObject == null) return;

            // 점프 상태(공중)에서만 벽 액션을 "진입"할 수 있음
            if (!actionCharacterBase.IsStatusJump())
            {
                // 지상으로 내려오면 정리
                if (_current != null)
                    ExitWall(forceRestorePrevious: true);
                return;
            }

            // 쿨다운 동안은 "재진입"만 막습니다. (진행 중 Phase를 강제 종료하지 않음)
            if (_current == null && Time.time < _cooldownUntil)
                return;

            // Phase가 없으면 (Hang으로) 진입 조건 평가
            if (_current == null)
            {
                if (!TryEnterHang(moveInput))
                    return;
            }

            // Phase별 공통 Tick 및 물리 Tick 수행
            _current?.Tick(this, moveInput);
            _current?.FixedTick(this, moveInput);
        }

        /// <summary>
        /// Jump 입력을 처리합니다. Hang/Slide 상태일 때만 WallJump Phase로 전환합니다.
        /// </summary>
        /// <param name="ctx">Input System 콜백 컨텍스트입니다.</param>
        /// <remarks>
        /// 공격/피격 등 우선순위 정책은 보통 InputManager에서 처리되지만,
        /// 본 메서드는 벽 점프 전이를 위한 최소한의 방어 로직을 포함합니다.
        /// </remarks>
        public void OnJump()
        {
            if (!_enabled) return;
            if (_current == null) return;

            // 벽 점프는 벽에 고정된 상태(Hang/Slide)에서만 허용
            if (_current.Id != WallPhaseId.Hang && _current.Id != WallPhaseId.Slide)
                return;

            // 공격/콤보 대기 중에는 벽 점프 전이를 막음 (정책 안전망)
            if (actionCharacterBase.IsStatusAttack()) return;
            if (actionCharacterBase.IsStatusAttackComboWait()) return;

            // 프로젝트 표준: 벽 점프는 공중 상태를 유지하도록 강제
            if (!actionCharacterBase.IsStatusJump())
                actionCharacterBase.SetStatusJump();

            SwitchToJump();
        }

        /// <summary>
        /// 벽 점프 직후 즉시 재부착되는 현상을 방지하기 위해 재부착 쿨다운을 강제로 부여합니다.
        /// </summary>
        public void ForceCooldown()
        {
            _cooldownUntil = Time.time + _reattachCooldown;
        }
    }
}
```
## Phase 인터페이스(표준 형태)

- 경로: `Action/Wall/WallPhase/IWallPhase.cs`
- 포인트:
  - Phase는 단일 책임
  - Enter/Tick/FixedTick/Exit 형태 유지

```csharp
    /// </summary>
    /// <remarks>
    /// - <see cref="Tick(ActionWall, Vector2)"/>: 프레임 단(입력/타이머/상태 전이 판단 등)
    /// - <see cref="FixedTick(ActionWall, Vector2)"/>: 물리 단(Rigidbody2D 이동/속도 적용 등)
    /// </remarks>
    internal interface IWallPhase
    {
        /// <summary>
        /// Phase 식별자입니다.
        /// </summary>
        WallPhaseId Id { get; }

        /// <summary>
        /// Phase 진입 시 1회 호출됩니다.
        /// </summary>
        /// <param name="ctx">벽 액션 컨텍스트입니다.</param>
        void Enter(ActionWall ctx);

        /// <summary>
        /// 매 프레임 호출되며, 입력 샘플링/타이머 누적/전이 판단 등을 수행합니다.
        /// </summary>
        /// <param name="ctx">벽 액션 컨텍스트입니다.</param>
        /// <param name="moveInput">이동 입력 벡터입니다.</param>
        void Tick(ActionWall ctx, Vector2 moveInput);

        /// <summary>
        /// FixedUpdate 타이밍으로 호출되며, Rigidbody2D 기반 물리 이동/속도 적용 등을 수행합니다.
        /// </summary>
        /// <param name="ctx">벽 액션 컨텍스트입니다.</param>
        /// <param name="moveInput">이동 입력 벡터입니다.</param>
        void FixedTick(ActionWall ctx, Vector2 moveInput);

        /// <summary>
        /// Phase 종료 시 1회 호출됩니다.
        /// </summary>
        /// <param name="ctx">벽 액션 컨텍스트입니다.</param>
        void Exit(ActionWall ctx);
    }
}
```
## Settings 분리(튜닝 외부화)

- 경로: `Action/Wall/ActionWallSettings.cs`
- 포인트:
  - 튜닝 값 하드코딩 금지
  - 디버그 플래그도 Settings로 제어

```csharp
    /// <remarks>
    /// - 설정 원본은 <c>playerActionSettings</c>(<see cref="GGemCoPlayerActionSettings"/>)이며,
    ///   런타임에서 빠르게 접근할 수 있도록 필드로 캐시합니다.
    /// - 센서(<c>_sensor</c>)의 레이어/거리 구성도 여기서 동기화합니다.
    /// </remarks>
    public partial class ActionWall
    {
        // --- Settings (cached from GGemCoPlayerActionSettings) ---

        /// <summary>Hang 상태에서 입력이 없을 때 Slide로 전환되기까지의 지연 시간(초)입니다.</summary>
        internal float HangToSlideDelay => _hangToSlideDelay;

        /// <summary>벽 점프 직후 재부착을 금지하는 쿨다운 시간(초)입니다.</summary>
        internal float ReattachCooldown => _reattachCooldown;

        /// <summary>벽에 매달릴 때 X축으로 벽 안쪽으로 파고드는(스냅) 보정값입니다.</summary>
        internal float WallHangInsetX => _wallHangInsetX;

        /// <summary>Slide 상태에서 아래로 떨어지는(미끄러지는) 속도입니다.</summary>
        internal float SlideDownSpeed => _slideDownSpeed;

        /// <summary>SlideEnd 종료 시 인계할 점프/탈출 속도의 X 성분입니다.</summary>
        internal float SlideEndExitJumpX => _slideEndExitJumpX;

        /// <summary>SlideEnd 종료 시 인계할 점프/탈출 속도의 Y 성분입니다.</summary>
        internal float SlideEndExitJumpY => _slideEndExitJumpY;

        /// <summary>벽 점프 방향을 구성할 각도(도)입니다. (2D 기준: 0°=+X, 90°=+Y)</summary>
        internal float WallJumpAngleDeg => _wallJumpAngleDeg;

        /// <summary>반대편 벽 목표가 존재할 때 사용할 벽 점프 속도입니다.</summary>
        internal float WallJumpSpeedWithOppositeWall => _wallJumpSpeedWithWall;

        /// <summary>반대편 벽 목표를 찾기 위한 예측 레이 길이(거리)입니다.</summary>
        internal float WallJumpPredictDistance => _wallJumpPredictDistance;

        /// <summary>벽 점프 Kinematic 시뮬레이션의 최대 지속 시간(초)입니다.</summary>
        internal float WallJumpMaxDuration => _wallJumpMaxDuration;

        /// <summary>JumpEnd 전환/종료를 판단할 목표 벽 접근 거리(또는 종료 판정 거리)입니다.</summary>
        internal float WallJumpEndDistance => _wallJumpEndDistance;

        /// <summary>JumpEnd 종료 시 인계할 점프/탈출 속도의 X 성분입니다.</summary>
        internal float WallJumpEndExitJumpX => _wallJumpEndExitJumpX;

        /// <summary>JumpEnd 종료 시 인계할 점프/탈출 속도의 Y 성분입니다.</summary>
        internal float WallJumpEndExitJumpY => _wallJumpEndExitJumpY;

        /// <summary>벽 판정에 사용할 레이어 마스크입니다.</summary>
        internal LayerMask WallMask => _wallMask;

        /// <summary>벽 판정(센서 체크/레이캐스트)에 사용할 거리입니다.</summary>
        internal float WallCheckDistance => _wallCheckDistance;

        // Anim

        /// <summary>Hang 애니메이션 이름(프리픽스 포함)입니다.</summary>
        internal string AnimHang => _animHang;

        /// <summary>Slide 애니메이션 이름(프리픽스 포함)입니다.</summary>
        internal string AnimSlide => _animSlide;

        /// <summary>WallJump 애니메이션 이름(프리픽스 포함)입니다.</summary>
        internal string AnimWallJump => _animWallJump;

        /// <summary>Hang 애니메이션이 실제로 존재하는지 여부입니다.</summary>
        internal bool HasHangAnim => _hasHang;

        /// <summary>Slide 애니메이션이 실제로 존재하는지 여부입니다.</summary>
        internal bool HasSlideAnim => _hasSlide;

        /// <summary>WallJump 애니메이션이 실제로 존재하는지 여부입니다.</summary>
        internal bool HasWallJumpAnim => _hasWallJump;

        /// <summary>
        /// Hang 에셋의 기본 바라보는 방향(리소스 기준)입니다.
        /// </summary>
        /// <remarks>
        /// 벽 위치(<c>wallSideX</c>)와 리소스 방향(<see cref="HangAssetFacingX"/>)을 비교하여
        /// 플립 여부를 결정합니다. (예: <c>wallSideX == assetFacingX</c>이면 Flip)
        /// </remarks>
        internal int HangAssetFacingX => _hangAssetFacingX;

        /// <summary>
        /// 플레이어 액션 설정(<c>playerActionSettings</c>)을 캐시하고, 센서/애니메이션 존재 여부를 동기화합니다.
        /// </summary>
        /// <remarks>
        /// - 벽 레이어 마스크가 0이면 기본값(타일맵 지형 레이어)을 사용합니다.
        /// - 애니메이션 프리픽스는 비어있으면 기본 문자열(wall_hang/wall_slide/wall_jump)을 사용합니다.
        /// </remarks>
        protected override void ApplySettings()
        {
            if (!playerActionSettings) return;

            _enabled = playerActionSettings.enableWallAction;

            _hangToSlideDelay = playerActionSettings.wallHangToSlideDelay;
            _reattachCooldown = playerActionSettings.wallReattachCooldown;
            _wallHangInsetX = playerActionSettings.wallHangInsetX;
            _slideDownSpeed = playerActionSettings.wallSlideDownSpeed;

            _slideEndExitJumpX = playerActionSettings.wallSlideEndExitJumpX;
            _slideEndExitJumpY = playerActionSettings.wallSlideEndExitJumpY;

            _wallJumpAngleDeg = playerActionSettings.wallJumpAngleDeg;
            _wallJumpSpeedWithWall = playerActionSettings.wallJumpSpeedWithOppositeWall;
            _wallJumpPredictDistance = playerActionSettings.wallJumpPredictDistance;
            _wallJumpMaxDuration = playerActionSettings.wallJumpMaxDuration;
            _wallJumpEndDistance = playerActionSettings.wallJumpEndDistance;
            _wallJumpEndExitJumpX = playerActionSettings.wallJumpEndExitJumpX;
            _wallJumpEndExitJumpY = playerActionSettings.wallJumpEndExitJumpY;

            _wallMask = playerActionSettings.wallMask;
            if (_wallMask == 0)
                _wallMask = LayerMask.GetMask(ConfigLayer.GetValue(ConfigLayer.Keys.TileMapGround));

            _wallCheckDistance = playerActionSettings.wallCheckDistance;

            // 센서가 이미 생성되어 있다는 전제(Initialize 이후 ApplySettings 호출)를 기반으로 구성값을 반영합니다.
            _sensor.Configure(_wallMask, _wallCheckDistance);

            _hangAssetFacingX =
                playerActionSettings.wallHangAssetFacing == GGemCoPlayerActionSettings.HangAnimationAssetFacing.Left
                    ? -1
                    : 1;

            // Animation prefixes
            _animHang = !string.IsNullOrWhiteSpace(playerActionSettings.prefixWallHangAnimation)
                ? playerActionSettings.prefixWallHangAnimation
                : "wall_hang";
            _animSlide = !string.IsNullOrWhiteSpace(playerActionSettings.prefixWallSlideAnimation)
                ? playerActionSettings.prefixWallSlideAnimation
                : "wall_slide";
            _animWallJump = !string.IsNullOrWhiteSpace(playerActionSettings.prefixWallJumpAnimation)
                ? playerActionSettings.prefixWallJumpAnimation
                : "wall_jump";

            var anim = actionCharacterBase.CharacterAnimationController;
            _hasHang = anim != null && anim.HasAnimation(_animHang);
            _hasSlide = anim != null && anim.HasAnimation(_animSlide);
            _hasWallJump = anim != null && anim.HasAnimation(_animWallJump);
        }

        // backing fields

        /// <summary>벽 액션 활성화 여부입니다.</summary>
        private bool _enabled;

        private float _hangToSlideDelay;
        private float _reattachCooldown;
        private float _wallHangInsetX;
        private float _slideDownSpeed;

        private float _slideEndExitJumpX;
        private float _slideEndExitJumpY;

        private float _wallJumpAngleDeg;
        private float _wallJumpSpeedWithWall;
        private float _wallJumpPredictDistance;
        private float _wallJumpMaxDuration;
        private float _wallJumpEndDistance;
        private float _wallJumpEndExitJumpX;
        private float _wallJumpEndExitJumpY;

        private LayerMask _wallMask;
        private float _wallCheckDistance;

        private string _animHang;
        private string _animSlide;
        private string _animWallJump;
        private bool _hasHang;
        private bool _hasSlide;
        private bool _hasWallJump;

        /// <summary>Hang 애니메이션 에셋의 기본 방향(-1: Left, +1: Right)입니다.</summary>
        private int _hangAssetFacingX;
    }
}
```
## PhaseSwitch(전환 조건 중앙화)

- 경로: `Action/Wall/ActionWallPhaseSwitch.cs`
- 포인트:
  - 전환 조건은 여기로 모아 if-else 누적 방지
  - 센서/입력/물리 조건을 한 곳에서 관리

```csharp
    ///   라이프사이클을 보장합니다.
    /// - 종료 시에는 상황에 따라 Rigidbody2D 물리 상태를 복구하거나(완전 복구),
    ///   Dynamic 물리로 인계하거나(기존 점프/낙하 시스템으로 자연스럽게 연결),
    ///   물리 상태를 유지한 채로(다른 액션이 제어) Wall 상태만 정리할 수 있습니다.
    /// </remarks>
    public partial class ActionWall
    {
        // Phases

        /// <summary>
        /// 현재 실행 중인 벽 Phase입니다. (없으면 null)
        /// </summary>
        private IWallPhase _current;

        /// <summary>벽 매달림 Phase 인스턴스입니다.</summary>
        private readonly IWallPhase _phaseHang = new WallPhaseHang();

        /// <summary>벽 미끄러짐 Phase 인스턴스입니다.</summary>
        private readonly IWallPhase _phaseSlide = new WallPhaseSlide();

        /// <summary>미끄러짐 종료(정리) Phase 인스턴스입니다.</summary>
        private readonly IWallPhase _phaseSlideEnd = new WallPhaseSlideEnd();

        /// <summary>벽 점프 Phase 인스턴스입니다.</summary>
        private readonly IWallPhase _phaseJump = new WallPhaseJump();

        /// <summary>벽 점프 종료(인계) Phase 인스턴스입니다.</summary>
        private readonly IWallPhase _phaseJumpEnd = new WallPhaseJumpEnd();

        /// <summary>
        /// 현재 Phase를 지정된 Phase로 전환합니다.
        /// </summary>
        /// <param name="next">전환할 다음 Phase입니다.</param>
        /// <exception cref="ArgumentNullException">next가 null이면 발생합니다.</exception>
        /// <remarks>
        /// 같은 인스턴스로의 전환은 무시하며,
        /// 전환 시 기존 Phase는 Exit → 다음 Phase는 Enter 순서로 호출합니다.
        /// </remarks>
        private void SwitchPhase(IWallPhase next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (_current == next) return;

            _current?.Exit(this);
            _current = next;
            _current.Enter(this);
        }

        /// <summary>현재 Phase를 Hang으로 전환합니다.</summary>
        internal void SwitchToHang() => SwitchPhase(_phaseHang);

        /// <summary>현재 Phase를 Slide로 전환합니다.</summary>
        internal void SwitchToSlide() => SwitchPhase(_phaseSlide);

        /// <summary>현재 Phase를 SlideEnd로 전환합니다.</summary>
        internal void SwitchToSlideEnd() => SwitchPhase(_phaseSlideEnd);

        /// <summary>현재 Phase를 Jump로 전환합니다.</summary>
        internal void SwitchToJump() => SwitchPhase(_phaseJump);

        /// <summary>현재 Phase를 JumpEnd로 전환합니다.</summary>
        internal void SwitchToJumpEnd() => SwitchPhase(_phaseJumpEnd);

        /// <summary>
        /// 벽이 위치한 방향(X 부호)을 설정합니다. (-1: 왼쪽, +1: 오른쪽, 0: 없음)
        /// </summary>
        /// <param name="sideX">벽 방향 후보 값입니다. 음수/양수는 각각 -1/+1로 정규화됩니다.</param>
        /// <remarks>
        /// 벽 방향이 바뀌면 앵커(<see cref="AnchorX"/>)를 무효화하여 누적 오차를 방지합니다.
        /// </remarks>
        internal void SetWallSideX(int sideX)
        {
            int next = sideX < 0 ? -1 : (sideX > 0 ? 1 : 0);

            if (WallSideX != next)
            {
                _hasAnchorX = false;
                _anchorX = float.NaN;
            }

            WallSideX = next;

            // 선호 방향이 미설정(0)인 경우 현재 벽 방향을 기본값으로 둡니다.
            if (_preferSideX == 0) _preferSideX = WallSideX;
        }

        /// <summary>
        /// 현재 캐릭터가 바라보는 방향을 X 부호(-1/+1)로 반환합니다.
        /// </summary>
        /// <returns>왼쪽을 보고 있으면 -1, 아니면 +1을 반환합니다.</returns>
        internal int GetFacingX()
        {
            return actionCharacterBase.CurrentFacing == CharacterConstants.FacingDirection8.Left ? -1 : 1;
        }

        /// <summary>
        /// 벽 액션을 종료하고, 필요 시 진입 이전 Rigidbody 상태로 복구하거나 Dynamic 물리로 인계합니다.
        /// </summary>
        /// <param name="forceRestorePrevious">
        /// true이면 진입 전 캐시된 Rigidbody 상태(bodyType/gravityScale/velocity)로 복구합니다.
        /// false이면 Dynamic으로 설정하여 기존 Jump/Fall 시스템이 자연스럽게 이어받도록 합니다.
        /// </param>
        /// <remarks>
        /// 종료 시 Phase 상태 및 내부 런타임 값을 초기화합니다.
        /// </remarks>
        internal void ExitWall(bool forceRestorePrevious)
        {
            _current?.Exit(this);
            _current = null;

            WallSideX = 0;
            _preferSideX = 0;
            NoInputTimeSeconds = 0f;
            _anchorX = float.NaN;
            _hasAnchorX = false;
            _jumpElapsed = 0f;
            _jumpVelocity = Vector2.zero;
            _jumpHasTargetWall = false;
            _jumpTargetPoint = Vector2.zero;

            if (_rigidbody == null) return;

            if (forceRestorePrevious)
            {
                _rigidbody.bodyType = _motionCache.PrevBodyType;
                _rigidbody.gravityScale = _motionCache.PrevGravityScale;
                _rigidbody.SetLinearVelocity(_motionCache.PrevVelocity);
            }
            else
            {
                // 기본은 Dynamic으로 넘겨, 기존 점프/낙하 로직이 이어받게 한다.
                _rigidbody.bodyType = RigidbodyType2D.Dynamic;
                _rigidbody.gravityScale = _motionCache.PrevGravityScale;
                _rigidbody.SetLinearVelocity(Vector2.zero);
            }
        }

        /// <summary>
        /// 벽 액션을 종료하고 Dynamic 물리로 전환한 뒤, 지정한 속도로 다음 액션에 인계합니다.
        /// </summary>
        /// <param name="handoffVelocity">인계할 초기 속도입니다.</param>
        /// <remarks>
        /// 예: JumpEnd에서 기존 점프/낙하 시스템에 특정 속도를 넘겨 자연스러운 연결을 만들 때 사용합니다.
        /// </remarks>
        internal void ExitWallToDynamic(Vector2 handoffVelocity)
        {
            _current?.Exit(this);
            _current = null;

            WallSideX = 0;
            _preferSideX = 0;
            NoInputTimeSeconds = 0f;
            _anchorX = float.NaN;
            _hasAnchorX = false;
            _jumpElapsed = 0f;
            _jumpVelocity = Vector2.zero;
            _jumpHasTargetWall = false;
            _jumpTargetPoint = Vector2.zero;

            if (_rigidbody == null) return;
            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.gravityScale = _motionCache.PrevGravityScale;
            _rigidbody.SetLinearVelocity(handoffVelocity);
        }

        /// <summary>
        /// Wall 액션 상태만 종료하고, Rigidbody2D의 물리 상태(bodyType/gravityScale/velocity)는 변경하지 않습니다.
        /// </summary>
        /// <remarks>
        /// JumpEnd처럼 다른 액션으로 물리 제어권을 인계할 때 사용합니다.
        /// </remarks>
        internal void ExitWallKeepCurrentPhysics()
        {
            _current?.Exit(this);
            _current = null;

            WallSideX = 0;
            _preferSideX = 0;
            NoInputTimeSeconds = 0f;

            // NOTE: 이 종료 경로는 "물리 상태 유지"가 핵심이므로 Anchor/Jump 관련 런타임 값만 정리합니다.
            _anchorX = 0f;
            _jumpElapsed = 0f;
            _jumpVelocity = Vector2.zero;
            _jumpHasTargetWall = false;
            _jumpTargetPoint = Vector2.zero;
        }

        /// <summary>
        /// 벽이 위치한 방향(X 부호)을 설정합니다. (0이면 해제)
        /// </summary>
        /// <param name="wallSideX">벽 방향 값입니다. 0이 아니면 부호만 사용합니다.</param>
        /// <remarks>
        /// 벽 반대편으로 전환되는 경우(예: WallJump로 반대편 벽에 도착)에는
        /// 기존 앵커를 유지하면 오차가 누적될 수 있으므로 앵커를 무효화합니다.
        /// </remarks>
        internal void SetWallSide(int wallSideX)
        {
            int next = wallSideX == 0 ? 0 : (int)Mathf.Sign(wallSideX);

            if (WallSideX != next)
            {
                _hasAnchorX = false;
                _anchorX = float.NaN;
            }

            WallSideX = next;
        }

        /// <summary>
        /// Hang 상태에서 캐릭터 스프라이트 플립을 적용합니다.
        /// </summary>
        /// <param name="wallSideX">현재 벽 방향(-1/+1)입니다.</param>
        /// <remarks>
        /// 벽 방향과 Hang 전용 에셋 기준 방향(<c>_hangAssetFacingX</c>)을 비교하여
        /// <see cref="CharacterBase.SetFlip(bool)"/>을 호출합니다.
        /// </remarks>
        internal void ApplyHangFlip(int wallSideX)
        {
            if (wallSideX == 0) return;
```
## AutoMoveAdapter(토큰 기반 Suspend/Resume)

- 경로: `Core/Input/AutoMoveAdapter.cs`
- 포인트:
  - Suspend 토큰은 기능별로 분리
  - Exit/Dispose에서 해제 짝을 반드시 보장
  - 동시 Suspend 충돌 방지

```csharp
    /// - 버튼 입력 차단 여부(Provider 정책)
    /// - Wall Action 진행 중 AutoMove Suspend(Resume 가능) 처리
    /// </summary>
    internal sealed class AutoMoveAdapter
    {
        private readonly IAutoMoveVectorProvider _provider;
        private readonly IAutoMoveSuspendService _suspend;

        private AutoMoveSuspendToken _wallSuspendToken;
        private AutoMoveSuspendToken _guardSuspendToken;
        private AutoMoveSuspendToken _playerAttackRangeSuspendToken;
        private AutoMoveSuspendToken _controlLockedSuspendToken;
        private bool _isSuspendedByWall;
        private bool _isSuspendedByGuard;
        private bool _isSuspendedByPlayerAttackRange;
        private bool _isSuspendedByControlLocked;

        public AutoMoveAdapter(IAutoMoveVectorProvider provider, IAutoMoveSuspendService suspend)
        {
            _provider = provider;
            _suspend = suspend;
            _wallSuspendToken = AutoMoveSuspendToken.None;
            _guardSuspendToken = AutoMoveSuspendToken.None;
            _playerAttackRangeSuspendToken = AutoMoveSuspendToken.None;
            _controlLockedSuspendToken = AutoMoveSuspendToken.None;
            _isSuspendedByWall = false;
            _isSuspendedByGuard = false;
            _isSuspendedByPlayerAttackRange = false;
            _isSuspendedByControlLocked = false;
        }

        public bool IsAutoMoveActive => _provider is { IsAutoMoveActive: true };

        /// <summary>
        /// AutoMove 활성 상태에서, 입력을 Provider에 통지하고 차단 여부를 반환합니다.
        /// </summary>
        public bool ShouldBlockInput(AutoMoveInputType inputType, Vector2 value)
        {
            if (_provider == null || !_provider.IsAutoMoveActive) return false;

            _provider.NotifyPlayerInput(inputType, value);
            return _provider.ShouldBlockInput(inputType);
        }

        /// <summary>
        /// Move 입력을 AutoMove 정책에 따라 오버라이드합니다.
        /// </summary>
        public Vector2 ResolveMove(Vector2 rawMove)
        {
            if (_provider == null || !_provider.IsAutoMoveActive) return rawMove;

            if (rawMove != Vector2.zero)
            {
                _provider.NotifyPlayerInput(AutoMoveInputType.Move, rawMove);
            }

            // 입력 통지 후에도 AutoMove가 유지될 수 있으므로 재확인
            return _provider.IsAutoMoveActive ? _provider.GetMoveVector() : rawMove;
        }

        /// <summary>
        /// Wall Action 진행 중에는 AutoMove를 Suspend(Resume 가능한 Pause)합니다.
        /// </summary>
        public void TickSuspendByWall(bool wallActive)
        {
            if (_suspend == null) return;

            if (wallActive)
            {
                if (!_isSuspendedByWall)
                {
                    _wallSuspendToken = _suspend.AcquireSuspend(AutoMoveSuspendReason.WallAction);
                    _isSuspendedByWall = _wallSuspendToken.IsValid;
                }
            }
            else
            {
                if (_isSuspendedByWall)
                {
                    _suspend.ReleaseSuspend(_wallSuspendToken);
                    _wallSuspendToken = AutoMoveSuspendToken.None;
                    _isSuspendedByWall = false;
                }
            }
        }
        public void TickSuspendByGuard(bool guardActive)
        {
            if (_suspend == null) return;

            if (guardActive)
            {
                if (!_isSuspendedByGuard)
                {
                    _guardSuspendToken = _suspend.AcquireSuspend(AutoMoveSuspendReason.GuardAction);
                    _isSuspendedByGuard = _guardSuspendToken.IsValid;
                }
            }
            else
            {
                if (_isSuspendedByGuard)
                {
                    _suspend.ReleaseSuspend(_guardSuspendToken);
                    _guardSuspendToken = AutoMoveSuspendToken.None;
                    _isSuspendedByGuard = false;
                }
            }
        }
        /// <summary>
        /// 플레이어 공격 거리에 몬스터가 있는 체크한 후 
        /// </summary>
        /// <param name="active"></param>
        public void TickSuspendByPlayerAttackRange(bool active)
        {
            if (_suspend == null) return;

            if (active)
            {
                if (!_isSuspendedByPlayerAttackRange)
                {
                    _playerAttackRangeSuspendToken = _suspend.AcquireSuspend(AutoMoveSuspendReason.PlayerAttackRange);
                    _isSuspendedByPlayerAttackRange = _playerAttackRangeSuspendToken.IsValid;
                }
            }
            else
            {
                if (_isSuspendedByPlayerAttackRange)
                {
                    _suspend.ReleaseSuspend(_playerAttackRangeSuspendToken);
                    _playerAttackRangeSuspendToken = AutoMoveSuspendToken.None;
                    _controlLockedSuspendToken = AutoMoveSuspendToken.None;
                    _isSuspendedByPlayerAttackRange = false;
                    _isSuspendedByControlLocked = false;
                }
            }
        }

        /// <summary>
        /// CharacterStatus.DontControl 등으로 제어가 잠겨 있을 때 AutoMove를 Suspend(Resume 가능한 Pause)합니다.
        /// </summary>
        public void TickSuspendByControlLocked(bool controlLocked)
        {
            if (_suspend == null) return;

            if (controlLocked)
            {
                if (!_isSuspendedByControlLocked)
                {
                    _controlLockedSuspendToken = _suspend.AcquireSuspend(AutoMoveSuspendReason.ControlLocked);
                    _isSuspendedByControlLocked = _controlLockedSuspendToken.IsValid;
                }
            }
            else
            {
                if (_isSuspendedByControlLocked)
                {
                    _suspend.ReleaseSuspend(_controlLockedSuspendToken);
                    _controlLockedSuspendToken = AutoMoveSuspendToken.None;
                    _isSuspendedByControlLocked = false;
                }
            }
        }

        public void ReleaseAll()
        {
            if (_suspend == null) return;
            if (_wallSuspendToken.IsValid)
            {
                _suspend.ReleaseSuspend(_wallSuspendToken);
                _wallSuspendToken = AutoMoveSuspendToken.None;
            }

            if (_guardSuspendToken.IsValid)
            {
                _suspend.ReleaseSuspend(_guardSuspendToken);
                _guardSuspendToken = AutoMoveSuspendToken.None;
            }

            if (_playerAttackRangeSuspendToken.IsValid)
            {
                _suspend.ReleaseSuspend(_playerAttackRangeSuspendToken);
                _playerAttackRangeSuspendToken = AutoMoveSuspendToken.None;
            }

            if (_controlLockedSuspendToken.IsValid)
            {
                _suspend.ReleaseSuspend(_controlLockedSuspendToken);
                _controlLockedSuspendToken = AutoMoveSuspendToken.None;
            }

            _isSuspendedByWall = false;
            _isSuspendedByGuard = false;
            _isSuspendedByPlayerAttackRange = false;
            _isSuspendedByControlLocked = false;
        }
    }
}
```
