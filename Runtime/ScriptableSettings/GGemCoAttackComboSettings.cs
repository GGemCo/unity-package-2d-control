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

            [Header("데미지 공식 설정")]
            [Tooltip("이 콤보 단계에서 사용할 데미지 공식 설정입니다. 커스텀 사용이 꺼져 있으면 기존 기본 물리 공격 공식을 사용합니다.")]
            public AttackComboDamageFormulaSettings damageFormula = AttackComboDamageFormulaSettings.Default;

            [Header("피격 인터럽트 설정")]
            [Tooltip("이 콤보 단계에서 피격 시 CC 중단 정책을 개별 설정할지 여부입니다.")]
            public bool overrideStopCrowdControlOnIncomingHit;

            [Tooltip("이 콤보 단계 중 피격되면 현재 적용 중이거나 예약된 Crowd Control을 중단합니다.")]
            public bool stopCrowdControlOnIncomingHit = true;

            [Tooltip("이 콤보 단계에서 피격 시 공격 전방 이동 중단 정책을 개별 설정할지 여부입니다.")]
            public bool overrideStopMoveForwardOnIncomingHit;

            [Tooltip("이 콤보 단계 중 피격되면 공격으로 시작한 전방 이동 보간을 중단합니다.")]
            public bool stopMoveForwardOnIncomingHit = true;

            [Header("HitStop 설정")]
            [Tooltip("이 공격이 실제로 명중했을 때 적용할 HitStop 설정입니다.")]
            public AttackHitStopSettings hitStop = AttackHitStopSettings.Disabled;

            [Header("Camera Shake 설정")]
            [Tooltip("이 공격이 실제로 명중했을 때 재생할 카메라 Shake 설정입니다.")]
            public AttackCameraShakeSettings cameraShake = AttackCameraShakeSettings.Disabled;

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

        [Header("피격 인터럽트 기본 설정")]
        [Tooltip("기본 콤보 공격 중 피격되면 현재 적용 중이거나 예약된 Crowd Control을 중단합니다.")]
        public bool stopCrowdControlOnIncomingHitDuringAttackCombo = true;

        [Tooltip("기본 콤보 공격 중 피격되면 공격으로 시작한 전방 이동 보간을 중단합니다.")]
        public bool stopMoveForwardOnIncomingHitDuringAttackCombo = true;
        
        public float GetWaitTime(int index)
        {
            if (attacks == null || index < 0 || index >= attacks.Count) return 0;
            return attacks[index].waitTime;
        }
        public float GetMoveForwardDistance(int index)
        {
            if (attacks == null || index < 0 || index >= attacks.Count) return 0;
            return attacks[index].moveForwardDistance;
        }

        public float GetMoveForwardSpeed(int index)
        {
            if (attacks == null || index < 0 || index >= attacks.Count) return 0;
            return attacks[index].moveForwardSpeed;
        }

        public string GetAnimationName(int index)
        {
            if (attacks == null || index < 0 || index >= attacks.Count) return "";
            return attacks[index].animationName;
        }

        public int GetCountCombo()
        {
            return attacks != null ? attacks.Count : 0;
        }

        public int GetAffectUid(int index)
        {
            if (attacks == null || index < 0 || index >= attacks.Count) return 0;
            return attacks[index].affectUid;
        }

        /// <summary>
        /// 지정한 콤보 인덱스에 설정된 데미지 공식 정책을 조회합니다.
        /// </summary>
        /// <param name="index">조회할 콤보 인덱스입니다.</param>
        /// <param name="settings">조회된 데미지 공식 설정입니다.</param>
        /// <returns>조회 가능한 콤보 단계가 있으면 <see langword="true"/>를 반환합니다.</returns>
        public bool TryGetDamageFormulaSettings(int index, out AttackComboDamageFormulaSettings settings)
        {
            settings = AttackComboDamageFormulaSettings.Default;
            if (attacks == null || index < 0 || index >= attacks.Count)
                return false;

            StruckAttackSetting attack = attacks[index];
            if (attack == null)
                return false;

            settings = attack.damageFormula;
            return true;
        }

        /// <summary>
        /// 지정한 콤보 인덱스에서 피격 시 Crowd Control을 중단해야 하는지 확인합니다.
        /// </summary>
        /// <param name="index">조회할 콤보 인덱스입니다.</param>
        /// <returns>피격 시 Crowd Control을 중단해야 하면 <see langword="true"/>를 반환합니다.</returns>
        /// <remarks>
        /// 콤보 단계에 개별 오버라이드가 설정되어 있으면 해당 값을 우선하고,
        /// 그렇지 않으면 전역 기본 정책인 <see cref="stopCrowdControlOnIncomingHitDuringAttackCombo"/> 값을 사용합니다.
        /// </remarks>
        public bool ShouldStopCrowdControlOnIncomingHit(int index)
        {
            if (attacks == null || index < 0 || index >= attacks.Count)
                return stopCrowdControlOnIncomingHitDuringAttackCombo;

            StruckAttackSetting attack = attacks[index];
            if (attack == null || !attack.overrideStopCrowdControlOnIncomingHit)
                return stopCrowdControlOnIncomingHitDuringAttackCombo;

            return attack.stopCrowdControlOnIncomingHit;
        }

        /// <summary>
        /// 지정한 콤보 인덱스에서 피격 시 공격 전방 이동을 중단해야 하는지 확인합니다.
        /// </summary>
        /// <param name="index">조회할 콤보 인덱스입니다.</param>
        /// <returns>피격 시 공격 전방 이동을 중단해야 하면 <see langword="true"/>를 반환합니다.</returns>
        /// <remarks>
        /// 콤보 단계에 개별 오버라이드가 설정되어 있으면 해당 값을 우선하고,
        /// 그렇지 않으면 전역 기본 정책인 <see cref="stopMoveForwardOnIncomingHitDuringAttackCombo"/> 값을 사용합니다.
        /// </remarks>
        public bool ShouldStopMoveForwardOnIncomingHit(int index)
        {
            if (attacks == null || index < 0 || index >= attacks.Count)
                return stopMoveForwardOnIncomingHitDuringAttackCombo;

            StruckAttackSetting attack = attacks[index];
            if (attack == null || !attack.overrideStopMoveForwardOnIncomingHit)
                return stopMoveForwardOnIncomingHitDuringAttackCombo;

            return attack.stopMoveForwardOnIncomingHit;
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

        /// <summary>
        /// 지정한 콤보 인덱스에 설정된 카메라 Shake 정책을 조회합니다.
        /// </summary>
        /// <param name="index">조회할 콤보 인덱스입니다.</param>
        /// <param name="settings">조회된 카메라 Shake 설정입니다.</param>
        /// <returns>사용 가능한 카메라 Shake 설정이 있으면 <see langword="true"/>를 반환합니다.</returns>
        public bool TryGetCameraShakeSettings(int index, out AttackCameraShakeSettings settings)
        {
            settings = AttackCameraShakeSettings.Disabled;
            if (attacks == null || index < 0 || index >= attacks.Count)
                return false;

            settings = attacks[index].cameraShake;
            return settings.HasCameraShake;
        }
    }
}
