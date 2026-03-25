using System;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// <see cref="ActionWall"/>의 Phase 전환 및 종료(인계) 로직을 포함하는 partial 구간입니다.
    /// </summary>
    /// <remarks>
    /// - Phase 전환은 <see cref="IWallPhase.Enter(ActionWall)"/> / <see cref="IWallPhase.Exit(ActionWall)"/> 호출을 통해
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
        private const int GravityOverridePriorityWall = 50;

        private void ApplyNoGravityDuringWallAction()
        {
            if (_rigidbody == null)
                return;

            if (_wallGravityOverrideHandle.IsValid || _hasWallFallbackGravityOverride)
                return;

            if (_physicsOverrideController == null && actionCharacterBase != null)
            {
                _physicsOverrideController = actionCharacterBase.PhysicsOverrideController;
            }

            if (_physicsOverrideController != null)
            {
                _wallGravityOverrideHandle = _physicsOverrideController.AcquireGravityOverride(
                    ownerKey: this,
                    lifecycleOwner: actionCharacterBase,
                    channel: CharacterPhysicsOverrideChannel.Action,
                    priority: GravityOverridePriorityWall,
                    gravityScale: 0f,
                    reason: "ActionWall");

                if (_wallGravityOverrideHandle.IsValid)
                    return;
            }

            _wallFallbackPrevGravityScale = _rigidbody.gravityScale;
            _rigidbody.gravityScale = 0f;
            _hasWallFallbackGravityOverride = true;
        }

        private void ReleaseWallGravityOverride()
        {
            if (_wallGravityOverrideHandle.IsValid && _physicsOverrideController != null)
            {
                _physicsOverrideController.ReleaseGravityOverride(ref _wallGravityOverrideHandle);
            }
            else if (_hasWallFallbackGravityOverride && _rigidbody != null)
            {
                _rigidbody.gravityScale = _wallFallbackPrevGravityScale;
            }

            _wallGravityOverrideHandle = default;
            _hasWallFallbackGravityOverride = false;
        }

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

            ReleaseWallGravityOverride();

            if (_rigidbody == null) return;

            if (forceRestorePrevious)
            {
                _rigidbody.bodyType = _motionCache.PrevBodyType;
                _rigidbody.SetLinearVelocity(_motionCache.PrevVelocity);
            }
            else
            {
                // 기본은 Dynamic으로 넘겨, 기존 점프/낙하 로직이 이어받게 한다.
                _rigidbody.bodyType = RigidbodyType2D.Dynamic;
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

            ReleaseWallGravityOverride();

            if (_rigidbody == null) return;
            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
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

            ReleaseWallGravityOverride();

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

            bool shouldFlip = wallSideX == _hangAssetFacingX;
            actionCharacterBase.SetFlip(shouldFlip);
        }

        /// <summary>
        /// 현재 Rigidbody2D의 주요 물리 상태를 <see cref="WallMotionCache"/>에 캐시합니다.
        /// </summary>
        /// <remarks>
        /// Exit/인계 시 원상복구 또는 연속 동작을 위해 사용됩니다.
        /// </remarks>
        internal void CacheMotionState()
        {
            if (_rigidbody == null) return;

            _motionCache = new WallMotionCache
            {
                PrevBodyType = _rigidbody.bodyType,
                PrevGravityScale = _physicsOverrideController != null
                    ? _physicsOverrideController.CurrentGravityScale
                    : _rigidbody.gravityScale,
                PrevVelocity = _rigidbody.GetLinearVelocity()
            };
        }

        /// <summary>
        /// 공중 상태에서 벽 감지 결과를 바탕으로 Hang Phase 진입을 시도합니다.
        /// </summary>
        /// <param name="moveInput">현재 이동 입력 벡터입니다.</param>
        /// <returns>진입에 성공하면 true, 조건이 맞지 않으면 false를 반환합니다.</returns>
        /// <remarks>
        /// - 입력 X가 있으면 입력 방향을 우선 선호 방향으로 사용합니다.
        /// - 입력이 없으면 현재 바라보는 방향을 선호 방향으로 사용합니다.
        /// - 선호 방향에서 벽이 감지되지 않으면 반대 방향을 재검사합니다.
        /// </remarks>
        private bool TryEnterHang(Vector2 moveInput)
        {
            // Hang으로 진입하는 순간의 Rigidbody 상태를 캐시해 두어
            // 이후 JumpEnd/Exit에서 원래 값(bodyType/velocity)을 정확히 복구할 수 있도록 합니다.
            CacheMotionState();

            // 방향: 입력이 있으면 입력 기반, 없으면 바라보는 방향 기반
            if (moveInput.x > 0.05f) _preferSideX = 1;
            else if (moveInput.x < -0.05f) _preferSideX = -1;
            else
                _preferSideX = actionCharacterBase.CurrentFacing == CharacterConstants.FacingDirection8.Left ? -1 : 1;

            var hit = _sensor.CheckSide(_preferSideX);
            if (!hit.IsHit)
            {
                hit = _sensor.CheckSide(-_preferSideX);
                if (!hit.IsHit)
                    return false;

                _preferSideX = -_preferSideX;
            }

            SetWallSide(_preferSideX);

            // 벽 액션 진입 시점의 Rigidbody 상태를 캐시한다.
            // (ExitWall/JumpEnd에서 원래 값으로 복구하는 기준)
            CacheMotionState();
            ApplyNoGravityDuringWallAction();

            SwitchToHang();
            return true;
        }
    }
}
