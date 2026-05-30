using System;
using System.Collections.Generic;
using UnityEngine;
using GGemCo2DCore;

namespace GGemCo2DControl
{
    /// <summary>
    /// 플레이어 콤보 공격 설정
    /// </summary>
    [CreateAssetMenu(fileName = ConfigScriptableObjectControl.AttackCombo.FileName, menuName = ConfigScriptableObjectControl.AttackCombo.MenuName, order = ConfigScriptableObjectControl.AttackCombo.Ordering)]
    public class GGemCoAttackComboSettings : ScriptableObject
    {
        [Serializable]
        public class StruckAttackSetting : ISerializationCallbackReceiver
        {
            [Header("애니메이션 설정")]
            [Tooltip("이 공격에서 재생될 애니메이션의 이름")]
            public string animationName;

            [Header("이동 설정")]
            [Tooltip("공격 시 앞으로 이동하는 거리 (단위: 유닛)")]
            public float moveForwardDistance;

            [Tooltip("공격 시 앞으로 이동하는 속도 (1이 기본값, 값이 낮을수록 더 빠름)")]
            public float moveForwardSpeed = 1f;

            [Header("플레이어 효과 설정")]
            [Tooltip("플레이어에게 적용되는 효과 (Affect UID)")]
            public int affectUid;

            [Header("HitStop 설정")]
            [Tooltip("이 공격이 실제로 명중했을 때 적용할 HitStop 설정입니다.")]
            public AttackHitStopSettings hitStop = AttackHitStopSettings.Disabled;

            [Tooltip("공격 후 다음 공격 입력이 가능해지기까지의 대기 시간 (단위: 초)")]
            public float waitTime;

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

        public int GetAffectUid(int index)
        {
            if (index < 0 || index >= attacks.Count) return 0;
            return attacks[index].affectUid;
        }


        /// <summary>
        /// 지정한 콤보 인덱스에 설정된 HitStop 정책을 조회합니다.
        /// </summary>
        /// <param name="index">조회할 콤보 인덱스입니다.</param>
        /// <param name="settings">조회된 HitStop 설정입니다.</param>
        /// <returns>사용 가능한 HitStop 설정이 있으면 <see langword="true"/>를 반환합니다.</returns>
        public bool TryGetHitStopSettings(int index, out AttackHitStopSettings settings)
        {
            settings = default;
            if (attacks == null || index < 0 || index >= attacks.Count)
                return false;

            settings = attacks[index].hitStop;
            return settings.HasAnyHitStop;
        }
    }
}
