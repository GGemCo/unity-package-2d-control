using GGemCo2DCore;

namespace GGemCo2DControl
{
    /// <summary>
    /// 방어 입력 처리.
    /// - AutoMove 차단은 상위(InputManager)에서 선처리
    /// - 상태/상충 캔슬 규칙은 <see cref="PlayerInputPolicy"/>에서 관리
    /// </summary>
    internal sealed class GuardInputHandler
    {
        private readonly CharacterBase _character;
        private readonly ActionGuard _guard;
        private readonly PlayerInputPolicy _policy;

        public GuardInputHandler(CharacterBase character, ActionGuard guard, PlayerInputPolicy policy)
        {
            _character = character;
            _guard = guard;
            _policy = policy;
        }

        public void HandlePress()
        {
            if (_character == null || _guard == null || _policy == null) return;

            if (!_policy.TryPrepareGuard(out var deny))
            {
                if (!string.IsNullOrEmpty(deny)) GcLogger.Log(deny);
                return;
            }

            _guard.GuardDown();
        }

        public void HandleRelease()
        {
            if (_character == null || _guard == null) return;
            _guard.GuardUp();
        }

        // (기존 chord 시스템과의 호환을 위해 남겨둠: 단발 호출이 들어오면 Press로 처리)
        public void Handle()
        {
            HandlePress();
        }
    }
}
