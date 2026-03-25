using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 반대편 벽이 없을 때 벽 액션을 종료하기 위한 종료 Phase입니다.
    /// </summary>
    /// <remarks>
    /// - Kinematic 상태에서 지정된 목표 지점(JumpTargetPoint)까지 이동합니다.
    /// - 목표 지점에 도달(또는 통과)하면 Dynamic으로 전환하며, "이탈 점프" 초기 속도를 부여해
    ///   기존 점프/낙하 로직(ActionJump 등)이 이어받도록 합니다.
    /// </remarks>
    internal sealed class WallPhaseJumpEnd : WallPhaseBase
    {
        /// <summary>
        /// 현재 Phase를 식별하는 고유 ID입니다.
        /// </summary>
        public override WallPhaseId Id => WallPhaseId.JumpEnd;

        /// <summary>
        /// JumpEnd Phase 진입 시, 목표 지점과 이동 속도를 구성하고 벽 재부착 방지 쿨다운을 적용합니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        public override void Enter(ActionWall ctx)
        {
            var rb = ctx.Rigidbody;

            // 종료 연출 구간은 Kinematic으로 직접 이동시키며(중력 제거), 물리 속도는 0으로 초기화합니다.
            if (rb.bodyType != RigidbodyType2D.Kinematic)
                rb.bodyType = RigidbodyType2D.Kinematic;

            rb.SetLinearVelocity(Vector2.zero);

            // 현재 벽의 반대 방향을 계산합니다(방향이 0인 경우를 방어적으로 처리).
            int wallSideX = ctx.WallSideX;
            int oppositeX = wallSideX == 0 ? 1 : (int)(-Mathf.Sign(wallSideX));

            // 점프 애니메이션이 있다면 재생합니다.
            if (ctx.HasWallJumpAnim)
                ctx.actionCharacterBase.CharacterAnimationController?.PlayCharacterAnimation(ctx.AnimWallJump);

            // 목표 지점: 현재 위치 기준, 각도/거리/반대 방향(oppositeX)으로 월드 좌표를 계산합니다.
            ctx.JumpTargetPoint =
                PolarPositionUtility.WorldFromLocalAngleDistance(
                    rb.position,
                    rb.gameObject.transform,
                    ctx.WallJumpAngleDeg,
                    ctx.WallJumpEndDistance,
                    oppositeX);

            // 목표 지점 방향으로 이동 속도를 설정합니다.
            Vector2 origin = rb.position;
            float rad = ctx.WallJumpAngleDeg * Mathf.Deg2Rad;
            Vector2 checkDir = new Vector2(oppositeX * Mathf.Cos(rad), Mathf.Sin(rad)).normalized;

            Vector2 toTarget = (ctx.JumpTargetPoint - origin);
            Vector2 dirToTarget = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : checkDir;

            // NOTE: JumpEnd에서도 동일한 속도 파라미터를 사용합니다(프로젝트 정책/튜닝 의도에 따름).
            ctx.JumpVelocity = dirToTarget * ctx.WallJumpSpeedWithOppositeWall;

            // 점프 중 원래 벽 재부착 방지(쿨다운). 진행 중 전이 강제 종료 방지는 ActionWall에서 처리합니다.
            ctx.ForceCooldown();
        }

        /// <summary>
        /// FixedUpdate 주기에서 목표 지점까지 Kinematic 이동을 진행하고, 도착 시 Dynamic으로 전환합니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        /// <param name="moveInput">플레이어의 이동 입력 값입니다(현재 Phase에서는 직접 사용하지 않습니다).</param>
        public override void FixedTick(ActionWall ctx, Vector2 moveInput)
        {
            var rb = ctx.Rigidbody;
            float dt = Time.fixedDeltaTime;

            // 목표 지점이 설정되지 않았다면(미설정/초기값) 진행하지 않습니다.
            // TODO: JumpTargetPoint가 (0,0)일 수 있는 월드 좌표라면 별도 플래그로 대체하는 것이 안전합니다.
            if (ctx.JumpTargetPoint == Vector2.zero)
                return;

            Vector2 origin = rb.position;
            Vector2 toTarget = ctx.JumpTargetPoint - origin;

            // 충분히 근접했다면 즉시 종료(이탈 점프 적용 후 Dynamic 인계)
            const float arriveEpsilon = 0.01f; // 월드 단위(튜닝 가능)
            if (toTarget.sqrMagnitude <= arriveEpsilon * arriveEpsilon)
            {
                ExitWithEndJump(ctx);
                return;
            }

            Vector2 delta = ctx.JumpVelocity * dt;

            // 이번 프레임 이동으로 목표점을 도달/통과할 수 있다면 도착 처리합니다.
            if (delta.sqrMagnitude >= toTarget.sqrMagnitude)
            {
                ExitWithEndJump(ctx);
                return;
            }

            // 아직 도착 전: 계속 이동(중력/물리 속도는 사용하지 않음)
            rb.MovePosition(origin + delta);
            rb.SetLinearVelocity(Vector2.zero);
        }

        /// <summary>
        /// Wall Action을 종료하고 Dynamic으로 전환하며, 이탈 점프 초기 속도를 부여합니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        private void ExitWithEndJump(ActionWall ctx)
        {
            // 현재 바라보는 방향 기준으로 X 속도를 결정하고, Y 속도는 설정값을 사용합니다.
            int dirX = ctx.GetFacingX();
            var v = new Vector2(dirX * ctx.WallJumpEndExitJumpX, ctx.WallJumpEndExitJumpY);

            ctx.ExitWallToDynamic(v);
        }
    }
}
