using System;
using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{

    /// <summary>
    /// 저스트 가드 성공 시 스테미나 소모량을 계산하는 정책입니다.
    /// </summary>
    public enum JustGuardStaminaCostPolicy
    {
        /// <summary>
        /// 저스트 가드 성공 시 스테미나를 소모하지 않습니다.
        /// </summary>
        None = 0,

        /// <summary>
        /// 설정된 고정값만큼 스테미나를 소모합니다.
        /// </summary>
        Fixed = 1,

        /// <summary>
        /// 최대 스테미나에 대한 비율로 스테미나를 소모합니다.
        /// </summary>
        PercentOfMax = 2,

        /// <summary>
        /// 일반 가드 성공 스테미나 소모량과 동일하게 소모합니다.
        /// </summary>
        SameAsGuardSuccess = 3,
    }

    /// <summary>
    /// 가드 시작 시 스테미나 비용 계산 방식을 정의합니다.
    /// </summary>
    public enum GuardStartStaminaCostPolicy
    {
        /// <summary>
        /// <see cref="GGemCoPlayerGuardSettings.guardStartStaminaCost"/> 값을 그대로 사용합니다.
        /// </summary>
        UseConfiguredValue = 0,

        /// <summary>
        /// 가드 시작 시 스테미나를 소모하지 않습니다.
        /// </summary>
        AlwaysFree = 1,

        /// <summary>
        /// <see cref="GGemCoPlayerGuardSettings.justGuardSuccessStaminaCostPolicy"/>가
        /// <see cref="JustGuardStaminaCostPolicy.None"/>일 때 가드 시작 스테미나를 소모하지 않습니다.
        /// </summary>
        FreeWhenJustGuardSuccessPolicyNone = 2,
    }

    /// <summary>
    /// 공격 중 가드 입력을 허용할 구간을 정의합니다.
    /// </summary>
    public enum AttackGuardCancelPolicy
    {
        /// <summary>
        /// 공격 상태에서는 가드 입력으로 공격을 취소하지 않습니다.
        /// </summary>
        None = 0,

        /// <summary>
        /// 공격 본 애니메이션이 끝난 뒤 콤보 대기 구간에서만 가드로 취소합니다.
        /// </summary>
        AttackComboWaitOnly = 1,

        /// <summary>
        /// 공격 본 애니메이션과 콤보 대기 구간 모두에서 가드로 취소합니다.
        /// </summary>
        AttackAndComboWait = 2,
    }

    /// <summary>
    /// 가드 판정 디버그 피드백을 화면에 표시하는 방식을 정의합니다.
    /// </summary>
    public enum GuardDebugFeedbackDisplayMode
    {
        /// <summary>
        /// 기존처럼 TextMeshPro 텍스트 오브젝트로 표시합니다.
        /// </summary>
        Text = 0,

        /// <summary>
        /// 설정된 스프라이트가 있으면 UI Image 오브젝트로 표시합니다.
        /// 스프라이트가 비어 있으면 텍스트 표시로 폴백합니다.
        /// </summary>
        Sprite = 1,
    }

    /// <summary>
    /// 가드 디버그 피드백 오브젝트의 X 좌표 기준을 정의합니다.
    /// </summary>
    public enum GuardDebugFeedbackXAxisPolicy
    {
        /// <summary>
        /// 기존 데미지 텍스트 위치와 랜덤 X 범위 규칙을 그대로 사용합니다.
        /// </summary>
        Current = 0,

        /// <summary>
        /// X 좌표를 플레이어 X 좌표에 고정하고 설정한 오프셋만 더해 표시합니다.
        /// </summary>
        PlayerXWithOffset = 1,
    }

    /// <summary>
    /// 플레이어 가드, 저스트 가드, 가드 브레이크, 스태미나 회복/탈진 정책을 관리하는 설정입니다.
    /// </summary>
    [CreateAssetMenu(fileName = ConfigScriptableObjectControl.PlayerGuard.FileName, menuName = ConfigScriptableObjectControl.PlayerGuard.MenuName, order = ConfigScriptableObjectControl.PlayerGuard.Ordering)]
    public sealed class GGemCoPlayerGuardSettings : ScriptableObject, ISettingsChangeNotifier
    {
        /// <summary>
        /// 가드 공격 타입별로 가드 결과와 추가 Crowd Control을 정의합니다.
        /// </summary>
        [Serializable]
        public sealed class GuardAttackTypeRule
        {
            [Tooltip("가드 시스템에서 분기할 공격 방어 타입입니다.")]
            public GuardAttackType attackType = GuardAttackType.Normal;

            [Tooltip("일반 가드 타이밍에서 이 타입 공격을 받았을 때의 결과입니다. None이면 가드가 성립하지 않습니다.")]
            public GuardResolutionOutcome guardOutcome = GuardResolutionOutcome.Guarded;

            [Tooltip("저스트 가드 타이밍에서 이 타입 공격을 받았을 때의 결과입니다. None이면 가드가 성립하지 않습니다.")]
            public GuardResolutionOutcome justGuardOutcome = GuardResolutionOutcome.JustGuarded;

            [Tooltip("일반 가드 결과일 때 추가로 적용할 crowd_control 테이블 Uid입니다. 0이면 적용하지 않습니다.")]
            public int guardedCrowdControlUid;

            [Tooltip("저스트 가드 결과일 때 추가로 적용할 crowd_control 테이블 Uid입니다. 0이면 적용하지 않습니다.")]
            public int justGuardedCrowdControlUid;

            [Tooltip("가드 브레이크 결과일 때 추가로 적용할 crowd_control 테이블 Uid입니다. 0이면 적용하지 않습니다.")]
            public int guardBrokenCrowdControlUid;

            /// <summary>
            /// 현재 저스트 가드 판정 여부를 기준으로 최종 가드 결과를 반환합니다.
            /// </summary>
            /// <param name="isJustGuard">저스트 가드 판정 구간이면 <see langword="true"/>입니다.</param>
            /// <returns>설정된 가드 판정 결과입니다.</returns>
            public GuardResolutionOutcome ResolveOutcome(bool isJustGuard)
            {
                return isJustGuard ? justGuardOutcome : guardOutcome;
            }

            /// <summary>
            /// 가드 결과에 맞는 Crowd Control UID를 반환합니다.
            /// </summary>
            /// <param name="outcome">최종 가드 판정 결과입니다.</param>
            /// <returns>적용할 Crowd Control UID입니다. 없으면 0입니다.</returns>
            public int ResolveCrowdControlUid(GuardResolutionOutcome outcome)
            {
                switch (outcome)
                {
                    case GuardResolutionOutcome.Guarded:
                        return guardedCrowdControlUid;
                    case GuardResolutionOutcome.JustGuarded:
                        return justGuardedCrowdControlUid;
                    case GuardResolutionOutcome.GuardBroken:
                        return guardBrokenCrowdControlUid;
                    default:
                        return 0;
                }
            }
        }

        /// <summary>
        /// 저스트 가드 성공 시 함께 재생할 추가 VFX 설정입니다.
        /// </summary>
        [Serializable]
        public sealed class GuardSuccessVfxEntry
        {
            [Tooltip("저스트 가드 성공 시 추가로 재생할 vfx_effect 테이블 Uid입니다. 0이면 재생하지 않습니다.")]
            public int vfxUid;

            [Tooltip("저스트 가드 성공 추가 VFX Sorting Layer입니다.")]
            public ConfigSortingLayer.Keys sortingLayer = ConfigSortingLayer.Keys.CharacterTop;

            [Tooltip("저스트 가드 성공 추가 VFX Sorting Order입니다.")]
            public int sortingOrder;

            [Tooltip("저스트 가드 성공 추가 VFX 위치 오프셋입니다. World 기준이며 캐릭터 좌우 방향에 따라 X 오프셋이 보정됩니다.")]
            public Vector3 offset = Vector3.zero;
        }

        // 에디터/플레이모드에서만 쓰일 런타임 이벤트 (직렬화 방지)
        public event Action Changed;

#if UNITY_EDITOR
        /// <summary>
        /// 인스펙터 값이 변경되었을 때 플레이 중 설정 변경을 즉시 반영합니다.
        /// </summary>
        private void OnValidate()
        {
            Changed?.Invoke();
        }
#endif

        /// <summary>
        /// 설정 변경 이벤트를 수동으로 발생시킵니다.
        /// </summary>
        public void RaiseChanged()
        {
            Changed?.Invoke();
        }

        [Header("방어(가드)")]
        [Tooltip("방어 애니메이션 prefix (예: guard)")]
        public string prefixGuardAnimation = "guard";

        [Tooltip("방어 시작시 차감되는 스테미나")]
        public long guardStartStaminaCost;

        [Tooltip("가드 시작 스테미나 비용 계산 정책입니다.")]
        public GuardStartStaminaCostPolicy guardStartStaminaCostPolicy = GuardStartStaminaCostPolicy.FreeWhenJustGuardSuccessPolicyNone;

        [Tooltip("일반 가드 성공시 차감되는 스테미나입니다. 저스트 가드 성공 비용은 아래 저스트 가드 설정을 사용합니다.")]
        public long guardSuccessStaminaCost;

        [Tooltip("가드를 하는 중이면, 몇 초 마다 차감할 것인지")]
        public float guardStaminaTickInterval;

        [Tooltip("guardStaminaTickInterval 시간마다 얼마나 차감할 것인지")]
        public long guardStaminaTickCost;

        [Header("가드 입력 캔슬")]
        [Tooltip("공격 중 가드 입력을 허용할 구간입니다. 기본값은 기존 동작 유지를 위해 공격 본 애니메이션과 콤보 대기 구간 모두에서 허용합니다.")]
        public AttackGuardCancelPolicy attackGuardCancelPolicy = AttackGuardCancelPolicy.AttackAndComboWait;

        [Header("방어(가드) - 피드백 연출")]
        [Tooltip("가드/저스트 가드 피드백 표시 여부")]
        public bool showGuardFeedback = true;

        [Tooltip("가드/저스트 가드 피드백을 텍스트 또는 스프라이트로 표시할지 결정합니다.")]
        public GuardDebugFeedbackDisplayMode guardFeedbackDisplayMode = GuardDebugFeedbackDisplayMode.Text;

        [Tooltip("일반 가드 성공 시 표시할 피드백 스프라이트입니다.")]
        public Sprite guardFeedbackSprite;

        [Tooltip("저스트 가드 성공 시 표시할 피드백 스프라이트입니다.")]
        public Sprite justGuardFeedbackSprite;

        [Tooltip("가드 브레이크 시 표시할 피드백 스프라이트입니다.")]
        public Sprite guardBreakFeedbackSprite;

        [Tooltip("피드백 스프라이트 표시 크기입니다. 0 이하면 스프라이트 원본 크기를 사용합니다.")]
        public Vector2 guardFeedbackSpriteSize = Vector2.zero;

        [Tooltip("일반 가드 성공 피드백에 재생할 ui_effect 데이터 테이블 UID입니다. 0이면 기본 플로팅 표시만 사용합니다.")]
        public int guardFeedbackUiEffectUid;

        [Tooltip("저스트 가드 성공 피드백에 재생할 ui_effect 데이터 테이블 UID입니다. 0이면 일반 가드 UID를 사용합니다.")]
        public int justGuardFeedbackUiEffectUid;

        [Tooltip("가드 브레이크 피드백에 재생할 ui_effect 데이터 테이블 UID입니다. 0이면 기본 플로팅 표시만 사용합니다.")]
        public int guardBreakFeedbackUiEffectUid;

        [Tooltip("가드 피드백 오브젝트의 X 좌표 기준 정책입니다.")]
        public GuardDebugFeedbackXAxisPolicy guardFeedbackXAxisPolicy = GuardDebugFeedbackXAxisPolicy.Current;

        [Tooltip("X 좌표 기준 정책이 PlayerXWithOffset일 때 플레이어 X 좌표에 더할 오프셋입니다.")]
        public float guardFeedbackPlayerXOffset;

        [Header("방어 성공 VFX")]
        [Tooltip("방어 성공 시 재생할 vfx_effect 테이블 Uid")]
        public int guardSuccessVfxUid;

        [Tooltip("방어 성공 VFX Sorting Layer")]
        public ConfigSortingLayer.Keys guardSuccessVfxSortingLayer = ConfigSortingLayer.Keys.CharacterTop;

        [Tooltip("방어 성공 VFX Sorting Order")]
        public int guardSuccessVfxSortingOrder;

        [Tooltip("방어 성공 VFX 위치 오프셋(World 기준)")]
        public Vector3 guardSuccessVfxOffset = Vector3.zero;

        [Header("저스트 가드 성공 VFX")]
        [Tooltip("저스트 가드 성공 시 재생할 vfx_effect 테이블 Uid")]
        public int justGuardSuccessVfxUid;

        [Tooltip("저스트 가드 성공 VFX Sorting Layer")]
        public ConfigSortingLayer.Keys justGuardSuccessVfxSortingLayer = ConfigSortingLayer.Keys.CharacterTop;

        [Tooltip("저스트 가드 성공 VFX Sorting Order")]
        public int justGuardSuccessVfxSortingOrder;

        [Tooltip("저스트 가드 성공 VFX 위치 오프셋(World 기준)")]
        public Vector3 justGuardSuccessVfxOffset = Vector3.zero;

        [Tooltip("저스트 가드 성공 시 단일 VFX와 함께 추가로 재생할 VFX 목록입니다.")]
        public List<GuardSuccessVfxEntry> additionalJustGuardSuccessVfxEntries = new();

        [Header("가드 성공 사운드")]
        [Tooltip("일반 가드 성공 시 재생할 sound 테이블 대표 UID입니다. 0이면 재생하지 않습니다.")]
        public int guardSuccessSoundUid;

        [Tooltip("저스트 가드 성공 시 재생할 sound 테이블 대표 UID입니다. 0이면 일반 가드 성공 사운드를 사용합니다.")]
        public int justGuardSuccessSoundUid;

        [Header("가드 브레이크")]
        [Tooltip("가드 브레이크 시 추가로 차감할 스테미나입니다. 공격별 설정이 0이면 이 값을 사용합니다.")]
        public long guardBreakStaminaCost;

        [Tooltip("가드 브레이크 이후 다시 가드 키를 눌러 가드에 진입할 때 필요한 스테미나입니다. -1이면 일반 가드 시작 비용을 사용하고, 0 이상이면 이 값을 사용합니다.")]
        public long guardBreakReGuardStaminaCost = -1L;

        [Tooltip("가드 브레이크 시 기본으로 적용할 데미지 배율입니다. 공격 메타데이터가 없을 때 사용합니다. 0=HP 피해 없음, 1=원래 데미지 모두 적용")]
        [Range(0f, 1f)]
        public float guardBreakDamageMultiplier = 0f;

        [Tooltip("가드 브레이크 시 기본으로 표시할 피드백 텍스트입니다.")]
        public string guardBreakFeedbackText = "GUARD BREAK";


        [Header("가드 브레이크 애니메이션 동기화")]
        [Tooltip("가드 브레이크 결과로 Crowd Control이 적용될 때, guard_break 애니메이션을 CC Duration에 맞춰 재생합니다.")]
        public bool syncGuardBreakAnimationToCrowdControl = true;

        [Tooltip("guard_break 클립이 CC Duration보다 긴 경우에만 TimeScale을 올립니다. 짧은 클립은 느리게 늘리지 않습니다.")]
        public bool onlySpeedUpGuardBreakAnimationWhenLonger = true;

        [Tooltip("guard_break 애니메이션에 적용할 최대 TimeScale입니다.")]
        [Min(1f)]
        public float guardBreakAnimationMaxTimeScale = 3f;

        [Tooltip("CC Duration이 너무 짧을 때 사용할 최소 동기화 시간입니다.")]
        [Min(0.01f)]
        public float guardBreakAnimationMinTargetDuration = 0.05f;

        [Tooltip("CC의 EaseType을 guard_break 애니메이션 재생 속도에도 적용합니다.")]
        public bool applyCrowdControlEasingToGuardBreakAnimation = true;

        [Header("가드 브레이크 VFX")]
        [Tooltip("가드 브레이크 시 재생할 vfx_effect 테이블 Uid. 공격별 VFX UID가 있으면 공격별 설정이 우선 적용됩니다.")]
        public int guardBreakVfxUid;

        [Tooltip("가드 브레이크 VFX Sorting Layer")]
        public ConfigSortingLayer.Keys guardBreakVfxSortingLayer = ConfigSortingLayer.Keys.CharacterTop;

        [Tooltip("가드 브레이크 VFX Sorting Order")]
        public int guardBreakVfxSortingOrder;

        [Tooltip("가드 브레이크 VFX 위치 오프셋(World 기준)")]
        public Vector3 guardBreakVfxOffset = Vector3.zero;

        [Header("저스트 가드")]
        [Tooltip("저스트 가드 기능 활성화 여부")]
        public bool enableJustGuard = true;

        [Tooltip("가드 시작 후 저스트 가드 판정을 열기까지의 지연 시간(초)")]
        public float justGuardOpenDelay = 0f;

        [Tooltip("저스트 가드 판정 유지 시간(초)")]
        public float justGuardWindowDuration = 0.12f;

        [Tooltip("일반 가드 성공 시 적용할 데미지 배율\n0 = 완전 방어, 1 = 감쇄 없음")]
        [Range(0f, 1f)]
        public float guardDamageMultiplier = 0f;

        [Tooltip("저스트 가드 성공 시 적용할 데미지 배율\n0 = 완전 방어, 1 = 감쇄 없음")]
        [Range(0f, 1f)]
        public float justGuardDamageMultiplier = 0f;

        [Header("저스트 가드 성공 스테미나")]
        [Tooltip("저스트 가드 성공 시 스테미나를 어떻게 소모할지 결정합니다. 기본값(None)은 소모하지 않습니다.")]
        public JustGuardStaminaCostPolicy justGuardSuccessStaminaCostPolicy = JustGuardStaminaCostPolicy.None;

        [Tooltip("저스트 가드 성공 스테미나 소모 값입니다. Fixed는 고정값, PercentOfMax는 최대 스테미나 대비 비율(0~1)로 사용합니다.")]
        [Min(0f)]
        public float justGuardSuccessStaminaCostValue;

        [Tooltip("공격이 캐릭터 정면에서 들어온 경우에만 가드/저스트가드를 허용할지 여부")]
        public bool guardFrontOnly = true;

        [Tooltip("일반 가드 성공 시 피격 리액션(피격 애니메이션/CC)을 막을지 여부")]
        public bool guardSuppressHitReaction = true;

        [Tooltip("저스트 가드 성공 시 피격 리액션(피격 애니메이션/CC)을 막을지 여부")]
        public bool justGuardSuppressHitReaction = true;

        [Header("공격 방어 타입별 처리")]
        [Tooltip("SkillDamageClip의 Guard Attack Type별로 가드 결과와 추가 CC를 설정합니다. 비어 있으면 기존 GuardInteractionMode 규칙을 사용합니다.")]
        public List<GuardAttackTypeRule> guardAttackTypeRules = new();

        [Header("스테미나 회복")]
        [Tooltip("방어를 하고 있지 않을 때, 몇 초 마다 회복할 것인지")]
        public float noGuardStaminaTickInterval;

        [Tooltip("틱 회복량 해석 방식\n- Flat: 고정값(정수 스테미나)\n- PercentOfMax: 최대 스테미나 대비 비율(0~1)\n  예) 0.02 = 2%")]
        public ConfigCommon.CalculateType noGuardStaminaTickValueType = ConfigCommon.CalculateType.Flat;

        [Tooltip("틱 회복량 값\n- Flat: 스테미나 값(정수로 반올림)\n- PercentOfMax: 최대 스테미나 대비 비율(0~1)")]
        public float noGuardStaminaTickValue;

        [Header("스테미나 탈진")]
        [Tooltip("스테미나가 0이 되었을 때 탈진 상태로 진입할지 여부")]
        public bool enableExhaustion = true;

        [Tooltip("탈진 유지 시간(초)\n- 이 시간 동안 전용 회복이 진행됩니다.")]
        public float exhaustionDurationSeconds = 2f;

        [Tooltip("탈진 중 회복할 최대 스테미나 비율(0~1)\n예) 0.3 = 최대 스테미나의 30%까지 회복")]
        [Range(0f, 1f)]
        public float exhaustionRecoverMaxRatio = 0.3f;

        [Tooltip("탈진 시작 애니메이션 이름")]
        public string exhaustionStartAnimation = "moveset_exhaustion_start";

        [Tooltip("탈진 루프 애니메이션 이름")]
        public string exhaustionLoopAnimation = "moveset_exhaustion_loop";

        [Tooltip("탈진 종료 애니메이션 이름")]
        public string exhaustionEndAnimation = "moveset_exhaustion_end";

        [Tooltip("탈진 중 피격으로 Damage 애니메이션에 들어간 뒤, 끝나면 다시 탈진 루프로 복귀할지 여부")]
        public bool resumeExhaustionLoopAfterHit = true;

        /// <summary>
        /// 공격 방어 타입에 대응하는 규칙을 조회합니다.
        /// </summary>
        /// <param name="attackType">스킬 데미지 클립에서 전달된 공격 방어 타입입니다.</param>
        /// <param name="rule">조회된 타입별 가드 규칙입니다.</param>
        /// <returns>설정된 규칙이 있으면 <see langword="true"/>입니다.</returns>
        public bool TryGetGuardAttackTypeRule(GuardAttackType attackType, out GuardAttackTypeRule rule)
        {
            rule = null;

            if (guardAttackTypeRules == null || guardAttackTypeRules.Count == 0)
                return false;

            for (int i = 0; i < guardAttackTypeRules.Count; i++)
            {
                GuardAttackTypeRule current = guardAttackTypeRules[i];
                if (current == null || current.attackType != attackType)
                    continue;

                rule = current;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 신규 생성 시 기본 공격 방어 타입 규칙을 구성합니다.
        /// </summary>
        private void Reset()
        {
            prefixGuardAnimation = "guard";
            syncGuardBreakAnimationToCrowdControl = true;
            onlySpeedUpGuardBreakAnimationWhenLonger = true;
            guardBreakAnimationMaxTimeScale = 3f;
            guardBreakAnimationMinTargetDuration = 0.05f;
            applyCrowdControlEasingToGuardBreakAnimation = true;
            guardBreakReGuardStaminaCost = -1L;
            guardStartStaminaCostPolicy = GuardStartStaminaCostPolicy.FreeWhenJustGuardSuccessPolicyNone;
            justGuardSuccessStaminaCostPolicy = JustGuardStaminaCostPolicy.None;
            justGuardSuccessStaminaCostValue = 0f;
            guardAttackTypeRules = new List<GuardAttackTypeRule>
            {
                new GuardAttackTypeRule
                {
                    attackType = GuardAttackType.Normal,
                    guardOutcome = GuardResolutionOutcome.Guarded,
                    justGuardOutcome = GuardResolutionOutcome.JustGuarded,
                },
                new GuardAttackTypeRule
                {
                    attackType = GuardAttackType.Medium,
                    guardOutcome = GuardResolutionOutcome.Guarded,
                    justGuardOutcome = GuardResolutionOutcome.JustGuarded,
                },
                new GuardAttackTypeRule
                {
                    attackType = GuardAttackType.Heavy,
                    guardOutcome = GuardResolutionOutcome.Guarded,
                    justGuardOutcome = GuardResolutionOutcome.JustGuarded,
                },
                new GuardAttackTypeRule
                {
                    attackType = GuardAttackType.Ultimate,
                    guardOutcome = GuardResolutionOutcome.GuardBroken,
                    justGuardOutcome = GuardResolutionOutcome.JustGuarded,
                },
            };
        }
    }
}
