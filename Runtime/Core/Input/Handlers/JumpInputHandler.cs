using GGemCo2DCore;

namespace GGemCo2DControl
{
    /// <summary>
    /// 점프 입력 처리.
    /// - 벽 매달림/미끄러짐 중에는 Wall Action으로 라우팅
    /// - 그 외 상태/상충 캔슬 규칙은 <see cref="PlayerInputPolicy"/>에서 관리
    /// </summary>
    internal sealed class JumpInputHandler
    {
        private readonly CharacterBase _character;
        private readonly ActionJump _jump;
        private readonly ActionGuard _guard;
        private readonly ActionWall _wall;
        private readonly PlayerInputPolicy _policy;

        /// <summary>
        /// 점프 입력 처리기를 생성합니다.
        /// </summary>
        /// <param name="character">입력을 적용할 플레이어 캐릭터입니다.</param>
        /// <param name="jump">점프 액션입니다.</param>
        /// <param name="guard">가드 액션입니다.</param>
        /// <param name="wall">벽 액션입니다.</param>
        /// <param name="policy">플레이어 입력 전이 정책입니다.</param>
        public JumpInputHandler(
            CharacterBase character,
            ActionJump jump,
            ActionGuard guard,
            ActionWall wall,
            PlayerInputPolicy policy)
        {
            _character = character;
            _jump = jump;
            _guard = guard;
            _wall = wall;
            _policy = policy;
        }

        /// <summary>
        /// 점프 입력을 검증하고 필요한 충돌 액션을 정리한 뒤 점프를 시작합니다.
        /// </summary>
        public void Handle()
        {
            if (_character == null || _jump == null || _policy == null) return;

            // 벽 매달림/미끄러짐 중에는 벽 점프로 라우팅
            if (_wall is { IsWallLocked: true })
            {
                _wall.OnJump();
                return;
            }

            // 점프 가능 여부를 먼저 확인하여, 공중 점프가 거부된 입력이 진행 중인 대시나 스킬을 취소하지 않게 합니다.
            if (!_jump.CanStartJump(out var jumpDeny))
            {
                if (!string.IsNullOrEmpty(jumpDeny)) GcLogger.Log(jumpDeny);
                return;
            }

            if (!_policy.TryPrepareJump(out var deny, out JumpPreparationContext preparationContext))
            {
                if (!string.IsNullOrEmpty(deny)) GcLogger.Log(deny);
                return;
            }

            bool suspendedGuard = preparationContext.SuspendGuardUntilLanding &&
                                  _guard != null &&
                                  _guard.TrySuspendUntilJumpLanding();

            if (_jump.TryJump())
            {
                return;
            }

            // 준비 직후 외부 상태가 바뀌어 점프 시작에 실패했다면 같은 프레임에 가드를 원상 복귀합니다.
            if (suspendedGuard)
            {
                _guard.TryResumeSuspendedGuard();
            }
        }
    }
}
