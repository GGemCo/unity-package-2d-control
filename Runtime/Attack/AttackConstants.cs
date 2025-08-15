using System.Collections.Generic;

namespace GGemCo2DControl
{
    public static class AttackConstants
    {
        public enum ActionType
        {
            None,
            Attack,
            Wait,
            SpecialAttack,
        }
        public enum AttackType
        {
            None,
            Normal,
            Strong,
            Special,
        }

        public const string NameAnimationAttackPrefix = "attack";
        public const int CountCombo = 3;
        public enum AttackCombo
        {
            None,
            Attack1,
            Attack2,
            Attack3,
        }
        public static readonly Dictionary<AttackCombo, string> AttackComboAnimNames = new Dictionary<AttackCombo, string>()
        {
            {AttackCombo.Attack1, "attack1"},
            {AttackCombo.Attack2, "attack2"},
            {AttackCombo.Attack3, "attack3"},
        };
        
        /*
         * GGemCoPlayerControlSettings
         *      - 공격 종류 개수 -> StruckAttackSetting
         *      - 개수 만큼 List 생성
         *
         *
         * StruckAttackSetting
         *      - 공격 애니메이션 이름
         *      - 공격 하면서 앞으로 가는 거리
         *      - 공격 데미지 + 몇 % 인지
         *      - 공격 후 대기시간
         *      - 피격 당한 몬스터 넉백 거리
         *      - 피격 당한 몬스터 넉백 시간
         *      -  
         * 
         */
    }
}