using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 벽을 따라 미끄러지는(Slide) Phase입니다.
    /// </summary>
    /// <remarks>
    /// - Rigidbody2D를 Kinematic으로 전환하고 중력을 제거한 뒤, 지정된 속도로 아래 방향 이동만 수행합니다.
    /// - 벽 접촉이 끊기면 SlideEnd로 전이하여 벽 이탈 연출/처리를 수행합니다.
    /// - 입력이 들어오면 Hang으로 전환하여 벽을 즉시 다시 잡습니다.
    /// </remarks>
    internal sealed class WallPhaseSlide : WallPhaseBase
    {
        /// <summary>
        /// 현재 Phase를 식별하는 고유 ID입니다.
        /// </summary>
        public override WallPhaseId Id => WallPhaseId.Slide;

        /// <summary>
        /// Slide Phase 진입 시 초기화를 수행하고, 중력 없이 미끄러지는 상태를 구성합니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        public override void Enter(ActionWall ctx)
        {
            // Hang에서 Slide로 전이될 수 있으므로, 무입력 누적 시간은 초기화합니다.
            ctx.NoInputTime = 0f;

            var rb = ctx.Rigidbody;

            // Slide는 물리 중력 대신 "지정 속도"로 하강하므로 Kinematic + 중력 0을 사용합니다.
            if (rb.bodyType != RigidbodyType2D.Kinematic)
                rb.bodyType = RigidbodyType2D.Kinematic;

            rb.gravityScale = 0f;
            rb.SetLinearVelocity(Vector2.zero);

            // Slide 애니메이션이 있다면 재생합니다.
            if (ctx.HasSlideAnim)
                ctx.actionCharacterBase.CharacterAnimationController?.PlayCharacterAnimation(ctx.AnimSlide);
        }

        /// <summary>
        /// 매 프레임 벽 접촉 여부와 입력 상태를 확인하여 Hang/SlideEnd 전이를 결정합니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        /// <param name="moveInput">플레이어의 이동 입력 값입니다.</param>
        public override void Tick(ActionWall ctx, Vector2 moveInput)
        {
            // 벽 접촉 유지 확인(현재 WallSideX 방향으로 센서 체크)
            var hit = ctx.Sensor.CheckSide(ctx.WallSideX);
            if (!hit.IsHit)
            {
                // 벽이 끊기면 SlideEnd로 전환하여 "살짝 점프하며" 벽을 벗어납니다.
                // (실제 Dynamic 전환/초기 속도 부여는 SlideEnd에서 수행)
                ctx.SwitchToSlideEnd();
                return;
            }

            // 입력이 들어오면 Hang으로 전환하여 즉시 벽을 다시 잡습니다.
            if (!HasNoMoveInput(moveInput))
            {
                ctx.NoInputTime = 0f;
                ctx.SwitchToHang();
                return;
            }
        }

        /// <summary>
        /// FixedUpdate 주기에서 중력 없이 지정 속도로 하강 이동을 수행합니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        /// <param name="moveInput">플레이어의 이동 입력 값입니다(현재 Phase에서는 직접 사용하지 않습니다).</param>
        public override void FixedTick(ActionWall ctx, Vector2 moveInput)
        {
            var rb = ctx.Rigidbody;

            float dt = Time.fixedDeltaTime;
            var p = rb.position;

            // 지정 속도만큼 아래로 이동합니다.
            p.y -= ctx.SlideDownSpeed * dt;

            rb.MovePosition(p);
        }

        /// <summary>
        /// Phase 종료 시 호출됩니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        public override void Exit(ActionWall ctx)
        {
            // 종료 시 복구(예: Dynamic 전환, 중력 복원 등)는 ctx.ExitWall()/ExitWallToDynamic() 정책에 위임합니다.
        }
    }
}
