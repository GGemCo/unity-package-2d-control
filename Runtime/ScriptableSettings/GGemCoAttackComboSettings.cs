using System;
using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DControl
{
    [CreateAssetMenu(fileName = ConfigScriptableObjectControl.AttackCombo.FileName, menuName = ConfigScriptableObjectControl.AttackCombo.MenuName, order = ConfigScriptableObjectControl.AttackCombo.Ordering)]
    public class GGemCoAttackComboSettings : ScriptableObject
    {
        [Serializable]
        public class StruckAttackSetting : ISerializationCallbackReceiver
        {
            [Header("애니메이션 이름")]
            public string animationName;
            [Header("공격시 앞으로 이동하는 거리")]
            public float moveForwardDistance;
            [Header("공격시 앞으로 이동하는 속도. 디폴트 1. 숫자가 작을수록 빠르게 이동합니다.")]
            public float moveForwardSpeed = 1f;
            [Header("최종 데미지의 % 추가")]
            public float addDamagePercent;
            [Header("공격 후 다음 공격할 수 있는 대기 시간(초)")]
            public float waitTime;
            [Header("몬스터가 피격시 뒤로 밀려나는 거리")]
            public float knockBackDistance;
            [Header("몬스터가 피격시 뒤로 밀려나는 시간(초)")]
            public float knockBackDuration;
            // 최초 초기화 여부를 표시 (인스펙터에는 숨김)
            [SerializeField, HideInInspector] private bool initialized;
            
            // 직렬화 직전 훅(여기서는 사용 안 함)
            public void OnBeforeSerialize() { }

            // 직렬화 해제 직후 훅: 새 항목 추가 시 딱 한 번 기본값을 보장
            public void OnAfterDeserialize()
            {
                if (initialized) return;
                if (moveForwardSpeed == 0f) // 사용자가 의도적으로 0을 넣은 케이스는 건드리지 않음
                    moveForwardSpeed = 1f;

                initialized = true;
            }
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

        public float GetMoveForwardSpeed(int index)
        {
            if (index < 0 || index >= attacks.Count) return 0;
            return attacks[index].moveForwardSpeed;
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

        public float GetAddAtk(int index)
        {
            if (index < 0 || index >= attacks.Count) return 0;
            return attacks[index].addDamagePercent;
        }
    }
}