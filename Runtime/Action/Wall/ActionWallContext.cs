using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// <see cref="ActionWall"/>의 Phase들이 공유하는 런타임 상태/컴포넌트 접근자를 정의하는 partial 구간입니다.
    /// </summary>
    /// <remarks>
    /// - Phase 구현체가 필요한 정보에 접근할 수 있도록 internal 프로퍼티로 노출합니다.
    /// - 외부(다른 시스템)에는 최소한만 공개하고, 벽 액션 내부 결합은 partial로 관리합니다.
    /// </remarks>
    public partial class ActionWall
    {
        // --- Components ---

        /// <summary>
        /// 벽 액션에서 물리 이동/속도 적용에 사용하는 Rigidbody2D입니다.
        /// </summary>
        internal Rigidbody2D Rigidbody => _rigidbody;

        /// <summary>
        /// 벽 접촉/감지에 사용하는 맵 오브젝트용 CapsuleCollider2D입니다.
        /// </summary>
        internal CapsuleCollider2D ColliderMapObject => _colliderMapObject;

        /// <summary>
        /// 벽 감지/스냅/레이캐스트 등을 담당하는 센서입니다.
        /// </summary>
        internal WallSensor2D Sensor => _sensor;

        /// <summary>
        /// 벽 액션에서 사용하는 중력 오버라이드 컨트롤러입니다.
        /// </summary>
        internal CharacterPhysicsOverrideController PhysicsOverrideController => _physicsOverrideController;

        // --- Runtime state (shared across phases) ---

        /// <summary>
        /// 벽 재부착/전이 판단에 사용하는 "선호 벽 방향"입니다. (-1: 왼쪽, +1: 오른쪽)
        /// </summary>
        internal int PreferSideX
        {
            get => _preferSideX;
            set => _preferSideX = value;
        }

        /// <summary>
        /// Phase 내부 로직에서 사용하는 "입력 없음" 누적 시간(초)입니다.
        /// </summary>
        /// <remarks>
        /// 외부 노출용 프로퍼티(<see cref="ActionWall.NoInputTimeSeconds"/>)와 동일한 값을 공유합니다.
        /// </remarks>
        internal float NoInputTime
        {
            get => NoInputTimeSeconds;
            set => NoInputTimeSeconds = value;
        }

        /// <summary>
        /// 벽에 고정될 때 사용할 X축 앵커(고정 좌표)입니다.
        /// </summary>
        internal float AnchorX
        {
            get => _anchorX;
            set => _anchorX = value;
        }

        /// <summary>
        /// <see cref="AnchorX"/>가 유효하게 설정되어 있는지 여부입니다.
        /// </summary>
        internal bool HasAnchorX
        {
            get => _hasAnchorX;
            set => _hasAnchorX = value;
        }

        /// <summary>
        /// 벽 액션 진입 전 Rigidbody2D 상태를 저장한 캐시입니다.
        /// </summary>
        internal WallMotionCache MotionCache
        {
            get => _motionCache;
            set => _motionCache = value;
        }

        /// <summary>
        /// 벽 점프(Kinematic 시뮬레이션)에서 사용되는 목표 속도 벡터입니다.
        /// </summary>
        internal Vector2 JumpVelocity
        {
            get => _jumpVelocity;
            set => _jumpVelocity = value;
        }

        /// <summary>
        /// 벽 점프 진행 시간(초) 누적값입니다.
        /// </summary>
        internal float JumpElapsed
        {
            get => _jumpElapsed;
            set => _jumpElapsed = value;
        }

        /// <summary>
        /// WallJump가 반대편 벽을 "목표"로 삼고 있는지 여부입니다.
        /// </summary>
        internal bool JumpHasTargetWall
        {
            get => _jumpHasTargetWall;
            set => _jumpHasTargetWall = value;
        }

        /// <summary>
        /// 반대편 벽 목표 좌표(예: 레이캐스트 히트 포인트)입니다.
        /// </summary>
        internal Vector2 JumpTargetPoint
        {
            get => _jumpTargetPoint;
            set => _jumpTargetPoint = value;
        }

        // --- backing fields ---

        /// <summary>물리 이동/속도 적용 대상 Rigidbody2D 참조입니다.</summary>
        private Rigidbody2D _rigidbody;

        /// <summary>벽 감지에 사용하는 맵 오브젝트용 CapsuleCollider2D 참조입니다.</summary>
        private CapsuleCollider2D _colliderMapObject;

        /// <summary>벽 감지/판정 센서 참조입니다.</summary>
        private WallSensor2D _sensor;

        /// <summary>벽 액션에서 사용하는 중력 오버라이드 컨트롤러입니다.</summary>
        private CharacterPhysicsOverrideController _physicsOverrideController;

        /// <summary>벽 액션이 점유 중인 중력 오버라이드 핸들입니다.</summary>
        private CharacterPhysicsOverrideHandle _wallGravityOverrideHandle;

        /// <summary>컨트롤러가 없을 때 사용할 fallback 이전 gravityScale 값입니다.</summary>
        private float _wallFallbackPrevGravityScale;

        /// <summary>fallback 중력 오버라이드 적용 여부입니다.</summary>
        private bool _hasWallFallbackGravityOverride;

        /// <summary>
        /// 벽 재부착 금지 쿨다운의 만료 시각(Time.time 기준)입니다.
        /// </summary>
        private float _cooldownUntil;

        /// <summary>선호 벽 방향(-1/ +1) 저장값입니다.</summary>
        private int _preferSideX;

        /// <summary>벽 고정용 X 앵커 좌표입니다.</summary>
        private float _anchorX;

        /// <summary><see cref="_anchorX"/>가 유효한지 여부입니다.</summary>
        private bool _hasAnchorX;

        /// <summary>진입 전 Rigidbody2D 상태 캐시입니다.</summary>
        private WallMotionCache _motionCache;

        /// <summary>벽 점프 중 목표 속도입니다.</summary>
        private Vector2 _jumpVelocity;

        /// <summary>벽 점프 경과 시간(초)입니다.</summary>
        private float _jumpElapsed;

        /// <summary>벽 점프가 반대편 벽 목표를 가지고 있는지 여부입니다.</summary>
        private bool _jumpHasTargetWall;

        /// <summary>반대편 벽 목표 좌표입니다.</summary>
        private Vector2 _jumpTargetPoint;
    }

    /// <summary>
    /// 벽 액션 진입/종료 시 Rigidbody2D의 주요 물리 상태를 저장/복구하기 위한 캐시 구조체입니다.
    /// </summary>
    /// <remarks>
    /// Phase에서 BodyType/중력/속도를 변경한 뒤, 종료 시 원상복구하는 용도로 사용합니다.
    /// </remarks>
    internal struct WallMotionCache
    {
        /// <summary>
        /// 진입 이전의 Rigidbody2D BodyType입니다.
        /// </summary>
        public RigidbodyType2D PrevBodyType;

        /// <summary>
        /// 진입 이전의 중력 배율(GravityScale)입니다.
        /// </summary>
        public float PrevGravityScale;

        /// <summary>
        /// 진입 이전의 속도(velocity)입니다.
        /// </summary>
        public Vector2 PrevVelocity;
    }
}
