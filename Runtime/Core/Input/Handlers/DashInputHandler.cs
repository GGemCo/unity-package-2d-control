using GGemCo2DCore;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// 대시 입력 처리.
    /// - 상태/상충 캔슬 규칙은 <see cref="PlayerInputPolicy"/>에서 관리
    /// </summary>
    internal sealed class DashInputHandler
    {
        private readonly CharacterBase _character;
        private readonly ActionDash _dash;
        private readonly PlayerInputPolicy _policy;

        public DashInputHandler(CharacterBase character, ActionDash dash, PlayerInputPolicy policy)
        {
            _character = character;
            _dash = dash;
            _policy = policy;
        }

        public void Handle(InputAction.CallbackContext ctx)
        {
            if (_character == null || _dash == null || _policy == null) return;
            if (!_policy.TryPrepareDash(out var deny))
            {
                if (!string.IsNullOrEmpty(deny)) GcLogger.Log(deny);
                return;
            }

            _dash.Dash(ctx);
        }
    }
}
