using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// Slide 도중 벽 접촉이 끊겼을 때 Wall 액션을 종료하기 위한 종료 Phase입니다.
    /// </summary>
    /// <remarks>
    /// - 현재 바라보는 방향으로 "이탈 점프" 초기 속도를 부여한 뒤 즉시 Dynamic으로 전환합니다.
    /// - 전환 이후의 점프/낙하 진행은 기존 Jump/Fall 시스템이 이어받습니다.
    /// </remarks>
    internal sealed class WallPhaseSlideEnd : WallPhaseBase
    {
        /// <summary>
        /// 현재 Phase를 식별하는 고유 ID입니다.
        /// </summary>
        public override WallPhaseId Id => WallPhaseId.SlideEnd;

        /// <summary>
        /// SlideEnd 진입 시 Wall 액션을 종료하고 Dynamic으로 전환하며, 이탈 점프 초기 속도를 적용합니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        public override void Enter(ActionWall ctx)
        {
            // 이탈 방향은 "현재 바라보는 방향"을 기준으로 결정합니다.
            // 이탈 점프 속도는 Settings(튜닝 값)에서 조정합니다.
            int dirX = ctx.GetFacingX();
            var v = new Vector2(dirX * ctx.SlideEndExitJumpX, ctx.SlideEndExitJumpY);

            // Wall 종료 직후 재부착을 방지하기 위한 쿨다운을 적용합니다.
            ctx.ForceCooldown();

            // Dynamic으로 넘기며, 이후 점프/낙하 로직은 외부 시스템이 이어받습니다.
            ctx.ExitWallToDynamic(v);
        }

        /// <summary>
        /// 매 프레임 호출됩니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        /// <param name="moveInput">플레이어의 이동 입력 값입니다.</param>
        public override void Tick(ActionWall ctx, Vector2 moveInput)
        {
            // Enter에서 즉시 종료/전환이 완료되므로 추가 처리하지 않습니다.
        }

        /// <summary>
        /// FixedUpdate 주기에서 호출됩니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        /// <param name="moveInput">플레이어의 이동 입력 값입니다.</param>
        public override void FixedTick(ActionWall ctx, Vector2 moveInput)
        {
            // Enter에서 즉시 종료/전환이 완료되므로 추가 처리하지 않습니다.
        }

        /// <summary>
        /// Phase 종료 시 호출됩니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        public override void Exit(ActionWall ctx)
        {
            // 종료 처리는 ctx.ExitWallToDynamic에서 수행합니다.
        }
    }
}
