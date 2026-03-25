using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 벽 점프(Wall Jump) Phase입니다. (Kinematic 기반 시뮬레이션)
    /// </summary>
    /// <remarks>
    /// - 점프 시작 시 Kinematic으로 전환하고 중력을 제거한 뒤, 목표 방향의 속도를 설정합니다.
    /// - 반대편 벽이 예측 거리 내에 존재하면 목표 벽으로 이동을 계속하며, 접촉 시 Hang으로 전이합니다.
    /// - 반대편 벽이 없으면 JumpEnd로 전이하여 Wall Action을 종료하고 외부 점프/낙하 로직이 이어받도록 합니다.
    /// </remarks>
    internal sealed class WallPhaseJump : WallPhaseBase
    {
        /// <summary>
        /// 현재 Phase를 식별하는 고유 ID입니다.
        /// </summary>
        public override WallPhaseId Id => WallPhaseId.Jump;

        /// <summary>
        /// Wall Jump를 시작하고, 반대편 벽의 존재 여부를 예측하여 Jump 유지 또는 JumpEnd 전이를 결정합니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        public override void Enter(ActionWall ctx)
        {
            var rb = ctx.Rigidbody;

            // 점프 동안은 Kinematic으로 직접 위치를 제어하고(물리 중력/가속 제거), 속도는 내부 값으로 관리합니다.
            if (rb.bodyType != RigidbodyType2D.Kinematic)
                rb.bodyType = RigidbodyType2D.Kinematic;

            rb.SetLinearVelocity(Vector2.zero);

            // 현재 벽의 반대 방향으로 "고정 거리" 내에 벽이 있는지 검사합니다.
            int wallSideX = ctx.WallSideX;
            int oppositeX = wallSideX == 0 ? 1 : (int)(-Mathf.Sign(wallSideX));

            // 요구사항: 각도 기반으로 반대편 벽을 예측합니다.
            float rad = ctx.WallJumpAngleDeg * Mathf.Deg2Rad;
            Vector2 origin = rb.position;

            // (oppositeX, angle)을 합성한 방향으로 레이를 발사해 목표 벽을 찾습니다.
            Vector2 checkDir = new Vector2(oppositeX * Mathf.Cos(rad), Mathf.Sin(rad)).normalized;

            ctx.RaycastNonAlloc(origin, checkDir, ctx.WallJumpPredictDistance, ctx.WallMask, out var hit);

            // 디버그 시각화를 위한 레이 정보 기록(실패/성공 및 hit point 포함)
#if UNITY_EDITOR            
            ctx.DebugSetJumpPredictRay(origin, checkDir, ctx.WallJumpPredictDistance, hit.collider != null, hit.point);
#endif
            // 반대편 벽이 없으면 Wall Action을 종료하는 JumpEnd로 전이합니다.
            if (!hit.collider)
            {
                ctx.SwitchToJumpEnd();
                return;
            }

            // 반대편 벽이 있으면 해당 방향을 새로운 벽 방향으로 설정합니다.
            ctx.SetWallSide(oppositeX);
            ctx.PreferSideX = oppositeX;

            // 목표 벽이 있는 점프: 목표 지점까지 "속도"로 이동하는 시뮬레이션을 유지합니다.
            ctx.JumpHasTargetWall = true;
            ctx.JumpTargetPoint = hit.point;
            ctx.JumpElapsed = 0f;

            // 목표 지점 방향으로 속도를 설정합니다.
            Vector2 toTarget = (ctx.JumpTargetPoint - origin);
            Vector2 dirToTarget = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : checkDir;

            ctx.JumpVelocity = dirToTarget * ctx.WallJumpSpeedWithOppositeWall;

            // 점프 애니메이션이 있다면 재생합니다.
            if (ctx.HasWallJumpAnim)
                ctx.actionCharacterBase.CharacterAnimationController?.PlayCharacterAnimation(ctx.AnimWallJump);

            // 점프 중 원래 벽 재부착 방지(쿨다운). 진행 중 강제 종료/전이 방지는 ActionWall에서 처리합니다.
            ctx.ForceCooldown();
        }

        /// <summary>
        /// FixedUpdate 주기에서 점프 이동을 진행하고, 목표 벽 접촉/최대 지속시간 등을 기준으로 전이를 처리합니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        /// <param name="moveInput">플레이어의 이동 입력 값입니다(현재 Phase에서는 직접 사용하지 않습니다).</param>
        public override void FixedTick(ActionWall ctx, Vector2 moveInput)
        {
            var rb = ctx.Rigidbody;
            float dt = Time.fixedDeltaTime;
            ctx.JumpElapsed += dt;

            // 목표 벽이 없는 케이스는 Enter에서 JumpEnd로 전이되므로 여기까지 오지 않지만, 방어적으로 처리합니다.
            if (!ctx.JumpHasTargetWall)
                return;

            // 중력 없는 속도 기반 이동(가상/내부 속도에 의해 이동)
            Vector2 delta = ctx.JumpVelocity * dt;
            Vector2 origin = rb.position;

            // 현재 바라보는 방향을 우선 벽 탐색 기준으로 삼습니다.
            var preferSideX = ctx.actionCharacterBase.CurrentFacing == CharacterConstants.FacingDirection8.Left ? -1 : 1;

            // 측면 센서로 벽 접촉을 확인하고, 접촉 시 Hang으로 전이합니다.
            var hit = ctx.Sensor.CheckSide(preferSideX, true);
            if (hit.IsHit)
            {
                ctx.SetWallSide(preferSideX);
                ctx.SwitchToHang();
                return;
            }

            // 아직 벽에 닿지 않았다면 계속 이동합니다.
            rb.MovePosition(origin + delta);
            rb.SetLinearVelocity(Vector2.zero);

            // 안전장치: 최대 지속시간이 지나면 Wall Action을 종료하고 Dynamic 점프/낙하 로직으로 제어권을 넘깁니다.
            if (ctx.JumpElapsed >= ctx.WallJumpMaxDuration)
            {
                ctx.JumpHasTargetWall = false;
                ctx.ExitWallToDynamic(ctx.JumpVelocity);
                return;
            }
        }

        /// <summary>
        /// Phase 종료 시 호출됩니다.
        /// </summary>
        /// <param name="ctx">벽 동작 처리에 필요한 런타임 컨텍스트입니다.</param>
        public override void Exit(ActionWall ctx)
        {
            // 종료 처리는 ctx.ExitWall / ctx.ExitWallToDynamic에서 수행합니다.
        }
    }
}
