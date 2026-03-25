using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 벽에 매달린(Hang) 상태를 처리하는 Phase입니다.
    /// - Rigidbody2D를 Kinematic으로 전환해 벽에 "붙는" 감각을 만듭니다.
    /// - 이동 입력이 없으면 일정 시간 후 Slide Phase로 전이합니다.
    /// </summary>
    internal sealed class WallPhaseHang : WallPhaseBase
    {
        /// <summary>
        /// 현재 Phase를 식별하는 고유 ID입니다.
        /// </summary>
        public override WallPhaseId Id => WallPhaseId.Hang;

        /// <summary>
        /// Hang Phase 진입 시 초기화 및 벽에 붙는 상태(정지/중력 제거/앵커 정렬)를 구성합니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        public override void Enter(ActionWall ctx)
        {
            ctx.NoInputTime = 0f;

            // 벽에 "고정"된 느낌을 위해 Kinematic + 중력 제거 + 속도 0으로 설정합니다.
            var rb = ctx.Rigidbody;
            if (rb.bodyType != RigidbodyType2D.Kinematic)
                rb.bodyType = RigidbodyType2D.Kinematic;

            rb.SetLinearVelocity(Vector2.zero);

            // 벽 방향에 맞춰 캐릭터 플립(좌/우) 및 관련 상태를 정렬합니다.
            ctx.ApplyHangFlip(ctx.WallSideX);

            ctx.SetWallSide(ctx.WallSideX);
            ctx.PreferSideX = ctx.WallSideX;

            // Hang 애니메이션이 있다면 재생합니다(컨트롤러가 없을 수 있으므로 null-조건 연산자 사용).
            if (ctx.HasHangAnim)
                ctx.actionCharacterBase.CharacterAnimationController?.PlayCharacterAnimation(ctx.AnimHang);

            // "벽에 붙는" 앵커 X를 계산/보존합니다.
            // - Slide → Hang 전환 시, Slide 단계에서 이미 X 정렬이 되었을 수 있습니다.
            // - 이때 앵커를 다시 계산하면 inset이 누적될 위험이 있어(= 다시 더함) 방지합니다.
            if (!ctx.HasAnchorX || float.IsNaN(ctx.AnchorX))
            {
                ctx.AnchorX = rb.position.x + (ctx.WallSideX * ctx.WallHangInsetX);
                ctx.HasAnchorX = true;
            }

            // 물리 위치를 앵커 X에 강제 정렬해 벽에서 미세하게 떨어져 보이는 현상을 줄입니다.
            var p = rb.position;
            if (Mathf.Abs(p.x - ctx.AnchorX) > 0.0001f)
            {
                p.x = ctx.AnchorX;
                rb.MovePosition(p);
            }
        }

        /// <summary>
        /// 매 프레임 입력을 관찰하여 Hang 유지/전이를 결정합니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        /// <param name="moveInput">플레이어의 이동 입력 값입니다.</param>
        public override void Tick(ActionWall ctx, Vector2 moveInput)
        {
            // 입력이 없으면 "무입력 시간"을 누적하고, 입력이 있으면 즉시 초기화합니다.
            if (HasNoMoveInput(moveInput))
                ctx.NoInputTime += Time.fixedDeltaTime;
            else
                ctx.NoInputTime = 0f;

            // 전이: 일정 시간 무입력 상태면 Slide Phase로 전환합니다.
            if (ctx.NoInputTime >= ctx.HangToSlideDelay)
            {
                ctx.SwitchToSlide();
            }
        }

        /// <summary>
        /// 물리 프레임(FixedUpdate)에서 호출됩니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        /// <param name="moveInput">플레이어의 이동 입력 값입니다.</param>
        public override void FixedTick(ActionWall ctx, Vector2 moveInput)
        {
            // 현재 Hang Phase에서는 물리 프레임에서 별도 처리를 하지 않습니다.
            // (필요 시 파생/수정하여 벽면 마찰, 미끄러짐, 포지션 보정 등을 추가할 수 있습니다.)
        }
    }
}
