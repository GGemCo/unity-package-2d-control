using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// 벽 관련 액션(매달림/미끄러짐/벽 점프 등)을 Phase(상태) 기반으로 관리하는 컨텍스트입니다.
    /// </summary>
    /// <remarks>
    /// - 실제 동작 로직은 Phase 클래스(<see cref="IWallPhase"/>)로 위임하고, 본 클래스는 진입/전이/공통 상태를 관리합니다.
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
        public bool IsKinematicWallJumping => _current is { Id: WallPhaseId.Jump };

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
        public void OnJump(InputAction.CallbackContext ctx)
        {
            if (!ctx.started) return;
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
