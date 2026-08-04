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
            if (TryHandlePress(out string denyLog))
            {
                return;
            }

            if (!string.IsNullOrEmpty(denyLog))
            {
                GcLogger.Log(denyLog);
            }
        }

        /// <summary>
        /// 현재 입력 정책을 적용해 가드 시작을 시도합니다.
        /// </summary>
        /// <param name="denyLog">가드 시작이 거부된 경우의 설명입니다.</param>
        /// <returns>가드 액션이 실제로 시작되면 <see langword="true"/>입니다.</returns>
        public bool TryHandlePress(out string denyLog)
        {
            denyLog = null;
            if (_character == null || _guard == null || _policy == null)
            {
                denyLog = "가드 입력 시스템이 아직 초기화되지 않았습니다.";
                return false;
            }

            // 동일한 홀드 입력이 중복 전달된 경우에는 기존 가드를 유지하고 실패 로그를 만들지 않습니다.
            if (_guard.IsGuarding)
            {
                return true;
            }

            if (!_policy.TryPrepareGuard(out denyLog, out GuardPreparationContext preparationContext))
            {
                return false;
            }

            if (_guard.TryGuardDown(preparationContext))
            {
                return true;
            }

            denyLog = "현재 캐릭터 상태 또는 스테미나 조건으로 가드를 시작할 수 없습니다.";
            return false;
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
