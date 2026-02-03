using GGemCo2DCore;
using UnityEngine.InputSystem;

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
        private readonly PlayerInputPolicy _policy;

        public AttackInputHandler(CharacterBase character, ActionAttack attack, PlayerInputPolicy policy)
        {
            _character = character;
            _attack = attack;
            _policy = policy;
        }

        public void Handle(InputAction.CallbackContext ctx)
        {
            if (_character == null || _attack == null || _policy == null) return;
            if (!_policy.TryPrepareAttack(out var deny))
            {
                if (!string.IsNullOrEmpty(deny)) GcLogger.Log(deny);
                return;
            }

            _attack.Attack(ctx);
        }
    }
}
