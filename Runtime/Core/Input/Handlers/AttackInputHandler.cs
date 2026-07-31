using GGemCo2DCore;

namespace GGemCo2DControl
{
    /// <summary>
    /// 공격 입력 처리.
    /// - AutoMove 차단은 상위(InputManager)에서 선처리
    /// - 상태/상충 캔슬 규칙은 <see cref="PlayerInputPolicy"/>에서 관리
    /// </summary>
    internal sealed class AttackInputHandler
    {
        private readonly CharacterBase _character;
        private readonly ActionAttack _attack;
        private readonly ActionGuard _guard;
        private readonly PlayerInputPolicy _policy;

        public AttackInputHandler(
            CharacterBase character,
            ActionAttack attack,
            ActionGuard guard,
            PlayerInputPolicy policy)
        {
            _character = character;
            _attack = attack;
            _guard = guard;
            _policy = policy;
        }

        /// <summary>
        /// 공격 정책을 검증하고 기본 공격이 실제 시작된 경우 충돌 중인 가드 상태를 즉시 종료합니다.
        /// </summary>
        public void Handle()
        {
            if (_character == null || _attack == null || _policy == null) return;
            if (!_policy.TryPrepareAttack(out var deny))
            {
                if (!string.IsNullOrEmpty(deny)) GcLogger.Log(deny);
                return;
            }

            if (_attack.TryAttack())
            {
                // 공격이 거부되거나 버퍼 처리만 된 경우에는 기존 가드를 유지해야 하므로 시작 성공 후 정리합니다.
                _guard?.CancelForConflictingAction();
            }
        }
    }
}
