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
        private readonly ActionWall _wall;
        private readonly PlayerInputPolicy _policy;

        public JumpInputHandler(CharacterBase character, ActionJump jump, ActionWall wall, PlayerInputPolicy policy)
        {
            _character = character;
            _jump = jump;
            _wall = wall;
            _policy = policy;
        }

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

            if (!_policy.TryPrepareJump(out var deny))
            {
                if (!string.IsNullOrEmpty(deny)) GcLogger.Log(deny);
                return;
            }

            _jump.TryJump();
        }
    }
}
