using System;
using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DControl
{
    [CreateAssetMenu(fileName = ConfigScriptableObjectControl.AttackCombo.FileName, menuName = ConfigScriptableObjectControl.AttackCombo.MenuName, order = ConfigScriptableObjectControl.AttackCombo.Ordering)]
    public class GGemCoAttackComboSettings : ScriptableObject
    {
        [Serializable]
        public class StruckAttackSetting
        {
            [Header("애니메이션 이름")]
            public string animationName;
            [Header("공격시 앞으로 이동하는 거리")]
            public float moveForwardDistance;
            [Header("최종 데미지의 % 추가")]
            public float addDamage;
            [Header("공격 후 다음 공격할 수 있는 대기 시간(초)")]
            public float waitTime;
            [Header("몬스터가 피격시 뒤로 밀려나는 거리")]
            public float knockBackDistance;
            [Header("몬스터가 피격시 뒤로 밀려나는 시간(초)")]
            public float knockBackDuration;
        }
        [Header("공격 셋팅")]
        public List<StruckAttackSetting> attacks;
        
        public float GetWaitTime(int index)
        {
            if (index < 0 || index >= attacks.Count) return 0;
            return attacks[index].waitTime;
        }
        public float GetMoveForwardDistance(int index)
        {
            if (index < 0 || index >= attacks.Count) return 0;
            return attacks[index].moveForwardDistance;
        }

        public string GetAnimationName(int index)
        {
            if (index < 0 || index >= attacks.Count) return "";
            return attacks[index].animationName;
        }

        public int GetCountCombo()
        {
            return attacks.Count;
        }
    }
}