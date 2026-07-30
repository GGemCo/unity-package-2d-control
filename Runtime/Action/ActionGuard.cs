using System;
using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 방어 Action
    /// - 버튼을 누르면 GuardStart → (완료 시) GuardWait(루프)로 진입
    /// - 버튼을 떼면 GuardEnd를 재생 후 Stop으로 종료
    /// </summary>
    public sealed class ActionGuard : ActionBase
    {
        public bool IsGuarding => _phase != GuardPhase.None;
        public bool IsActivelyGuarding => _phase == GuardPhase.Start || _phase == GuardPhase.Wait;

        /// <summary>
        /// 가드 브레이크 이후 가드 키 Release가 들어오기 전까지 재가드를 막아야 하는지 여부입니다.
        /// </summary>
        public bool RequiresReleaseBeforeReGuard => _requiresReleaseBeforeReGuard;

        private enum GuardPhase
        {
            None,
            Start,
            Wait,
            End,
            Break,
        }

        private GuardPhase _phase;

        private string _animGuardStart;
        private string _animGuardWait;
        private string _animGuardEnd;
        private string _animGuardSuccess;
        private string _animGuardBreak;

        // --- 애니메이션 존재 여부 캐시 ---
        private bool _hasStart, _hasWait, _hasEnd, _hasSuccess, _hasBreak;
        private float _guardSuccessDurationSeconds;
        private float _guardBreakDurationSeconds;
        private bool _isGuardSuccessAnimationPlaying;
        private float _guardSuccessAnimationElapsed;
        private float _guardBreakAnimationElapsed;
        
        // [Tooltip("방어 시작시 차감되는 스테미나")]
        private long _guardStartStaminaCost;
        private GuardStartStaminaCostPolicy _guardStartStaminaCostPolicy;

        // [Tooltip("방어 성공시 차감되는 스테미나")]
        private long _guardSuccessStaminaCost;

        // [Tooltip("저스트 가드 성공시 스테미나 소모 정책")]
        private JustGuardStaminaCostPolicy _justGuardSuccessStaminaCostPolicy;

        // [Tooltip("저스트 가드 성공시 차감되는 스테미나 값")]
        private float _justGuardSuccessStaminaCostValue;

        // [Tooltip("가드를 하는 중이면, 몇 초 마다 차감할 것인지")]
        private float _guardStaminaTickInterval;

        // [Tooltip("guardStaminaTickInterval 시간마다 얼마나 차감할 것인지")]
        private long _guardStaminaTickCost;
        private int _guardSuccessVfxUid;
        private ConfigSortingLayer.Keys _guardSuccessVfxSortingLayer;
        private int _guardSuccessVfxSortingOrder;
        private Vector3 _guardSuccessVfxOffset;
        private int _justGuardSuccessVfxUid;
        private ConfigSortingLayer.Keys _justGuardSuccessVfxSortingLayer;
        private int _justGuardSuccessVfxSortingOrder;
        private Vector3 _justGuardSuccessVfxOffset;
        private List<GGemCoPlayerGuardSettings.GuardSuccessVfxEntry> _additionalJustGuardSuccessVfxEntries;
        private int _guardSuccessSoundUid;
        private int _justGuardSuccessSoundUid;
        private long _guardBreakStaminaCost;
        private long _guardBreakReGuardStaminaCost;
        private float _guardBreakDamageMultiplier;
        private string _guardBreakFeedbackText;
        private int _guardBreakVfxUid;
        private ConfigSortingLayer.Keys _guardBreakVfxSortingLayer;
        private int _guardBreakVfxSortingOrder;
        private Vector3 _guardBreakVfxOffset;
        private bool _syncGuardBreakAnimationToCrowdControl;
        private bool _onlySpeedUpGuardBreakAnimationWhenLonger;
        private float _guardBreakAnimationMaxTimeScale;
        private float _guardBreakAnimationMinTargetDuration;
        private bool _applyCrowdControlEasingToGuardBreakAnimation;
        private float _guardBreakAnimationActiveDurationSeconds;

        // 스테미나 틱 누적(프레임 드랍 보정)
        private float _staminaTickElapsed;

        private bool _enableJustGuard;
        private float _justGuardOpenDelay;
        private float _justGuardWindowDuration;
        private float _guardDamageMultiplier;
        private float _justGuardDamageMultiplier;
        private bool _guardFrontOnly;
        private bool _guardSuppressHitReaction;
        private bool _justGuardSuppressHitReaction;
        private GuardDebugFeedbackDisplayMode _guardDebugFeedbackDisplayMode;
        private Sprite _guardDebugFeedbackSprite;
        private Sprite _justGuardDebugFeedbackSprite;
        private Sprite _guardBreakDebugFeedbackSprite;
        private Vector2 _guardDebugFeedbackSpriteSize;
        private int _guardDebugFeedbackUiEffectUid;
        private int _justGuardDebugFeedbackUiEffectUid;
        private int _guardBreakDebugFeedbackUiEffectUid;
        private GuardDebugFeedbackXAxisPolicy _guardDebugFeedbackXAxisPolicy;
        private float _guardDebugFeedbackPlayerXOffset;

        private float _guardStartedTime = -999f;
        private bool _isCharacterStop;
        private bool _requiresReleaseBeforeReGuard;
        private bool _isAwaitingGuardBreakReGuard;

        /// <summary>
        /// 현재 가드 입력 버튼이 물리적으로 눌린 상태인지 추적합니다.
        /// CC 중 입력 이벤트가 제한되어도, CC 종료 후 guard_wait 복귀 여부를 안정적으로 판단하기 위해 사용합니다.
        /// </summary>
        private bool _isGuardInputHeld;

        /// <summary>
        /// 조작 불가 상태에서 가드 키 Release가 들어와, CC 종료 후 가드 상태를 정리해야 하는지 여부입니다.
        /// </summary>
        private bool _pendingReleaseAfterControlUnlock;

        /// <summary>
        /// Guarded 결과로 CC가 적용된 뒤, 입력이 유지 중이면 CC 종료 후 guard_wait로 복귀해야 하는지 여부입니다.
        /// </summary>
        private bool _pendingResumeGuardWaitAfterControlUnlock;

        public override void Initialize(InputManager inputManager, CharacterBase characterBase, CharacterBaseController characterBaseController)
        {
            // ApplySettings에서 playerGuardSettings 사용
            base.Initialize(inputManager, characterBase, characterBaseController);
            actionCharacterBase.OnAnimationEventGuardEnd += OnAnimationEventGuardEnd;
            // 탈진 시스템을 사용하면, CharacterBase.Stop 처리를 하지 않는다.
            _isCharacterStop = !(playerGuardSettings && playerGuardSettings.enableExhaustion);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            actionCharacterBase.OnAnimationEventGuardEnd -= OnAnimationEventGuardEnd;
        }

        protected override void ApplySettings()
        {
            ApplyGuardAnimationNames();
            InitializeGuardSettings();
        }

        private void ApplyGuardAnimationNames()
        {
            // Settings가 없거나 값이 비어있으면 기본값으로 폴백
            var prefix = playerGuardSettings != null && !string.IsNullOrWhiteSpace(playerGuardSettings.prefixGuardAnimation)
                ? playerGuardSettings.prefixGuardAnimation
                : "guard";

            _animGuardStart = prefix;
            _animGuardWait = prefix + "_wait";
            _animGuardEnd = prefix + "_end";
            _animGuardSuccess = prefix + "_success";
            _animGuardBreak = prefix + "_break";
            _hasStart = HasAnimation(_animGuardStart);
            _hasWait = HasAnimation(_animGuardWait);
            _hasEnd = HasAnimation(_animGuardEnd);
            _hasSuccess = HasAnimation(_animGuardSuccess);
            _hasBreak = HasAnimation(_animGuardBreak);

            _guardSuccessDurationSeconds = 0f;
            _guardBreakDurationSeconds = 0f;
            if (actionCharacterBase?.CharacterAnimationController == null) return;

            if (_hasSuccess)
            {
                _guardSuccessDurationSeconds = Mathf.Max(
                    0f,
                    actionCharacterBase.CharacterAnimationController.GetCharacterAnimationDuration(_animGuardSuccess, false));
            }

            if (_hasBreak)
            {
                _guardBreakDurationSeconds = Mathf.Max(
                    0f,
                    actionCharacterBase.CharacterAnimationController.GetCharacterAnimationDuration(_animGuardBreak, false));
            }
        }

        /// <summary>
        /// 가드 전용 설정 자산에서 런타임 캐시 값을 초기화합니다.
        /// </summary>
        private void InitializeGuardSettings()
        {
            if (!playerGuardSettings) return;
            _guardStartStaminaCost = playerGuardSettings.guardStartStaminaCost;
            _guardStartStaminaCostPolicy = playerGuardSettings.guardStartStaminaCostPolicy;
            _guardSuccessStaminaCost = playerGuardSettings.guardSuccessStaminaCost;
            _justGuardSuccessStaminaCostPolicy = playerGuardSettings.justGuardSuccessStaminaCostPolicy;
            _justGuardSuccessStaminaCostValue = Mathf.Max(0f, playerGuardSettings.justGuardSuccessStaminaCostValue);
            _guardStaminaTickInterval = playerGuardSettings.guardStaminaTickInterval;
            _guardStaminaTickCost = playerGuardSettings.guardStaminaTickCost;
            _guardSuccessVfxUid = playerGuardSettings.guardSuccessVfxUid;
            _guardSuccessVfxSortingLayer = playerGuardSettings.guardSuccessVfxSortingLayer;
            _guardSuccessVfxSortingOrder = playerGuardSettings.guardSuccessVfxSortingOrder;
            _guardSuccessVfxOffset = playerGuardSettings.guardSuccessVfxOffset;
            _justGuardSuccessVfxUid = playerGuardSettings.justGuardSuccessVfxUid;
            _justGuardSuccessVfxSortingLayer = playerGuardSettings.justGuardSuccessVfxSortingLayer;
            _justGuardSuccessVfxSortingOrder = playerGuardSettings.justGuardSuccessVfxSortingOrder;
            _justGuardSuccessVfxOffset = playerGuardSettings.justGuardSuccessVfxOffset;
            _additionalJustGuardSuccessVfxEntries = playerGuardSettings.additionalJustGuardSuccessVfxEntries;
            _guardSuccessSoundUid = Mathf.Max(0, playerGuardSettings.guardSuccessSoundUid);
            _justGuardSuccessSoundUid = Mathf.Max(0, playerGuardSettings.justGuardSuccessSoundUid);
            _guardBreakStaminaCost = playerGuardSettings.guardBreakStaminaCost;
            _guardBreakReGuardStaminaCost = Math.Max(-1L, playerGuardSettings.guardBreakReGuardStaminaCost);
            _guardBreakDamageMultiplier = Mathf.Clamp01(playerGuardSettings.guardBreakDamageMultiplier);
            _guardBreakFeedbackText = string.IsNullOrWhiteSpace(playerGuardSettings.guardBreakFeedbackText)
                ? "GUARD BREAK"
                : playerGuardSettings.guardBreakFeedbackText;
            _guardBreakVfxUid = playerGuardSettings.guardBreakVfxUid;
            _guardBreakVfxSortingLayer = playerGuardSettings.guardBreakVfxSortingLayer;
            _guardBreakVfxSortingOrder = playerGuardSettings.guardBreakVfxSortingOrder;
            _guardBreakVfxOffset = playerGuardSettings.guardBreakVfxOffset;
            _syncGuardBreakAnimationToCrowdControl = playerGuardSettings.syncGuardBreakAnimationToCrowdControl;
            _onlySpeedUpGuardBreakAnimationWhenLonger = playerGuardSettings.onlySpeedUpGuardBreakAnimationWhenLonger;
            _guardBreakAnimationMaxTimeScale = Mathf.Max(1f, playerGuardSettings.guardBreakAnimationMaxTimeScale);
            _guardBreakAnimationMinTargetDuration = Mathf.Max(0.01f, playerGuardSettings.guardBreakAnimationMinTargetDuration);
            _applyCrowdControlEasingToGuardBreakAnimation = playerGuardSettings.applyCrowdControlEasingToGuardBreakAnimation;

            _enableJustGuard = playerGuardSettings.enableJustGuard;
            _justGuardOpenDelay = Mathf.Max(0f, playerGuardSettings.justGuardOpenDelay);
            _justGuardWindowDuration = Mathf.Max(0f, playerGuardSettings.justGuardWindowDuration);
            _guardDamageMultiplier = Mathf.Clamp01(playerGuardSettings.guardDamageMultiplier);
            _justGuardDamageMultiplier = Mathf.Clamp01(playerGuardSettings.justGuardDamageMultiplier);
            _guardFrontOnly = playerGuardSettings.guardFrontOnly;
            _guardSuppressHitReaction = playerGuardSettings.guardSuppressHitReaction;
            _justGuardSuppressHitReaction = playerGuardSettings.justGuardSuppressHitReaction;
            _guardDebugFeedbackDisplayMode = playerGuardSettings.guardFeedbackDisplayMode;
            _guardDebugFeedbackSprite = playerGuardSettings.guardFeedbackSprite;
            _justGuardDebugFeedbackSprite = playerGuardSettings.justGuardFeedbackSprite;
            _guardBreakDebugFeedbackSprite = playerGuardSettings.guardBreakFeedbackSprite;
            _guardDebugFeedbackSpriteSize = playerGuardSettings.guardFeedbackSpriteSize;
            _guardDebugFeedbackUiEffectUid = Mathf.Max(0, playerGuardSettings.guardFeedbackUiEffectUid);
            _justGuardDebugFeedbackUiEffectUid = Mathf.Max(0, playerGuardSettings.justGuardFeedbackUiEffectUid);
            _guardBreakDebugFeedbackUiEffectUid = Mathf.Max(0, playerGuardSettings.guardBreakFeedbackUiEffectUid);
            _guardDebugFeedbackXAxisPolicy = playerGuardSettings.guardFeedbackXAxisPolicy;
            _guardDebugFeedbackPlayerXOffset = playerGuardSettings.guardFeedbackPlayerXOffset;
            _isCharacterStop = !(playerGuardSettings && playerGuardSettings.enableExhaustion);
        }

        /// <summary>
        /// Guard 버튼 Down(Pressed) 처리.
        /// </summary>
        public void GuardDown()
        {
            GuardDown(interruptHitStopOnStart: false);
        }

        /// <summary>
        /// Guard 버튼 Down 입력을 처리하고, 가드 시작 성공 시 필요에 따라 활성 HitStop을 종료합니다.
        /// </summary>
        /// <param name="interruptHitStopOnStart">
        /// 스테미나 지불 후 실제 가드 진입이 확정되면 활성 HitStop을 종료할지 여부입니다.
        /// </param>
        internal void GuardDown(bool interruptHitStopOnStart)
        {
            if (actionCharacterBase == null) return;
            if (actionCharacterBase.IsStatusDead()) return;

            _isGuardInputHeld = true;

            // 가드 브레이크 이후에는 사용자가 가드 키를 한 번 뗀 뒤 다시 눌러야 합니다.
            if (_requiresReleaseBeforeReGuard) return;

            if (_phase == GuardPhase.Break)
            {
                TryBeginGuardAfterGuardBreakReInput();
                return;
            }

            // 이미 가드 중이면 유지 (중복 호출 방지)
            if (IsGuarding) return;

            TryBeginGuardWithCost(
                ResolveCurrentGuardStartStaminaCost(),
                cancelGuardBreakAnimation: false,
                interruptHitStopOnStart: interruptHitStopOnStart);
        }

        /// <summary>
        /// Update 루프에서 호출되는 Guard 유지 처리
        /// - 스테미나 0이면 입력과 무관하게 즉시 해제
        /// - 설정된 주기마다 스테미나를 차감
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!IsGuarding) return;
            if (actionCharacterBase == null) return;

            // 사망 시 즉시 해제
            if (actionCharacterBase.IsStatusDead())
            {
                CancelGuard(true, false);
                return;
            }

            if (TryProcessPendingReleaseAfterControlUnlock())
            {
                return;
            }

            if (_phase == GuardPhase.Break)
            {
                TickGuardBreakAnimation(deltaTime);
                return;
            }

            // 스테미나 0이면 입력과 무관하게 즉시 해제
            if (actionCharacterBase.CurrentStamina.Value <= 0)
            {
                CancelGuard(true, _isCharacterStop);
                return;
            }

            TickGuardSuccessAnimation(deltaTime);

            if (TryProcessPendingResumeGuardWaitAfterControlUnlock())
            {
                return;
            }

            // 틱 차감 비활성
            if (_guardStaminaTickInterval <= 0f || _guardStaminaTickCost <= 0f)
            {
                return;
            }

            _staminaTickElapsed += deltaTime;
            if (_staminaTickElapsed < _guardStaminaTickInterval) return;

            // 프레임 드랍에도 누락되지 않도록 while 처리
            while (_staminaTickElapsed >= _guardStaminaTickInterval)
            {
                _staminaTickElapsed -= _guardStaminaTickInterval;

                if (!TrySpendStamina(_guardStaminaTickCost))
                {
                    // 유지 불가 → 즉시 해제
                    CancelGuard(true, _isCharacterStop);
                    return;
                }

                if (actionCharacterBase.CurrentStamina.Value <= 0)
                {
                    CancelGuard(true, _isCharacterStop);
                    return;
                }
            }
        }

        /// <summary>
        /// 가드 성공(블록/저스트가드 등)이 확정된 시점에 호출.
        /// - 성공 비용을 차감
        /// - 부족하면 즉시 가드 해제 후 false 반환
        /// </summary>
        /// <param name="isJustGuard">저스트 가드 성공 여부입니다. true이면 저스트 가드 전용 VFX 우선 정책을 적용합니다.</param>
        public bool OnGuardSuccess(bool isJustGuard)
        {
            if (!IsGuarding) return false;
            if (actionCharacterBase == null) return false;

            long staminaCost = ResolveGuardSuccessStaminaCost(isJustGuard);
            if (!TrySpendStamina(staminaCost))
            {
                CancelGuard(true, _isCharacterStop);
                return false;
            }

            if (actionCharacterBase.CurrentStamina.Value <= 0)
            {
                CancelGuard(true, _isCharacterStop);
                return false;
            }

            TryPlayGuardSuccessAnimation();
            TryPlayGuardSuccessVfx(isJustGuard);
            TryPlayGuardSuccessSound(isJustGuard);
            return true;
        }

        /// <summary>
        /// 가드 성공 결과를 같은 플레이어 오브젝트의 상위 시스템 포트에 전달합니다.
        /// </summary>
        /// <param name="metadataDamage">가드 판정에 사용된 데미지 메타데이터입니다.</param>
        /// <param name="result">가드 성공 판정 결과입니다.</param>
        private void NotifyGuardSuccessFeedback(MetadataDamage metadataDamage, GuardResolutionResult result)
        {
            if (!result.IsResolved)
                return;
            if (result.Outcome != GuardResolutionOutcome.Guarded &&
                result.Outcome != GuardResolutionOutcome.JustGuarded)
                return;
            if (actionCharacterBase == null)
                return;

            var feedback = new PlayerGuardSuccessFeedback(
                actionCharacterBase.gameObject,
                metadataDamage != null ? metadataDamage.attacker : null,
                metadataDamage,
                result.IsJustGuard,
                result.Outcome,
                Time.time);

            var behaviours = actionCharacterBase.GetComponents<MonoBehaviour>();
            if (behaviours == null || behaviours.Length == 0)
                return;

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerGuardSuccessFeedbackSink sink)
                {
                    sink.NotifyPlayerGuardSuccess(in feedback);
                }
            }
        }

        /// <summary>
        /// 가드 성공 타입에 따라 실제로 소모할 스테미나 값을 계산합니다.
        /// </summary>
        /// <param name="isJustGuard">저스트 가드 성공 여부입니다.</param>
        /// <returns>이번 가드 성공에 필요한 스테미나 소모량입니다.</returns>
        private long ResolveGuardSuccessStaminaCost(bool isJustGuard)
        {
            if (!isJustGuard)
                return Math.Max(0L, _guardSuccessStaminaCost);

            switch (_justGuardSuccessStaminaCostPolicy)
            {
                case JustGuardStaminaCostPolicy.None:
                    return 0;

                case JustGuardStaminaCostPolicy.Fixed:
                    return Mathf.Max(0, Mathf.RoundToInt(_justGuardSuccessStaminaCostValue));

                case JustGuardStaminaCostPolicy.PercentOfMax:
                    return ResolveJustGuardPercentStaminaCost();

                case JustGuardStaminaCostPolicy.SameAsGuardSuccess:
                    return Math.Max(0L, _guardSuccessStaminaCost);

                default:
                    return 0;
            }
        }

        /// <summary>
        /// 가드 시작 시 소모할 스테미나 비용을 정책에 따라 계산합니다.
        /// </summary>
        /// <remarks>
        /// <see cref="GuardStartStaminaCostPolicy.FreeWhenJustGuardSuccessPolicyNone"/> 정책에서는
        /// 저스트 가드 성공 스테미나 정책이 <see cref="JustGuardStaminaCostPolicy.None"/>일 때
        /// 시작 스테미나 비용도 함께 0으로 처리합니다.
        /// </remarks>
        /// <returns>가드 시작 시 실제로 소모할 스테미나 비용입니다.</returns>
        private long ResolveGuardStartStaminaCost()
        {
            long configuredCost = Math.Max(0L, _guardStartStaminaCost);

            switch (_guardStartStaminaCostPolicy)
            {
                case GuardStartStaminaCostPolicy.AlwaysFree:
                    return 0L;

                case GuardStartStaminaCostPolicy.FreeWhenJustGuardSuccessPolicyNone:
                    return _justGuardSuccessStaminaCostPolicy == JustGuardStaminaCostPolicy.None
                        ? 0L
                        : configuredCost;

                case GuardStartStaminaCostPolicy.UseConfiguredValue:
                default:
                    return configuredCost;
            }
        }

        /// <summary>
        /// 현재 가드 시작 맥락에 맞는 스테미나 비용을 계산합니다.
        /// </summary>
        /// <remarks>
        /// 가드 브레이크 이후 첫 재가드 입력에는 전용 비용을 우선 사용하고, 전용 비용이 음수이면 일반 가드 시작 비용을 사용합니다.
        /// </remarks>
        /// <returns>현재 가드 시작 시 필요한 스테미나 비용입니다.</returns>
        private long ResolveCurrentGuardStartStaminaCost()
        {
            return _isAwaitingGuardBreakReGuard
                ? ResolveGuardBreakReGuardStaminaCost()
                : ResolveGuardStartStaminaCost();
        }

        /// <summary>
        /// 가드 브레이크 이후 재가드 입력에 필요한 스테미나 비용을 계산합니다.
        /// </summary>
        /// <returns>가드 브레이크 이후 재가드에 필요한 스테미나 비용입니다.</returns>
        private long ResolveGuardBreakReGuardStaminaCost()
        {
            return _guardBreakReGuardStaminaCost >= 0L
                ? _guardBreakReGuardStaminaCost
                : ResolveGuardStartStaminaCost();
        }

        /// <summary>
        /// 가드 브레이크 이후 사용자가 가드 키를 다시 눌렀을 때 재가드 진입을 시도합니다.
        /// </summary>
        /// <remarks>
        /// 필요한 스테미나가 충분하면 브레이크 애니메이션을 즉시 끊고 가드 시작 흐름으로 들어갑니다.
        /// 부족하면 브레이크 애니메이션과 재가드 대기 상태를 그대로 유지합니다.
        /// </remarks>
        /// <returns>재가드 진입에 성공하면 true입니다.</returns>
        private bool TryBeginGuardAfterGuardBreakReInput()
        {
            return TryBeginGuardWithCost(ResolveGuardBreakReGuardStaminaCost(), cancelGuardBreakAnimation: true);
        }

        /// <summary>
        /// 지정한 스테미나 비용을 지불한 뒤 가드 시작 상태로 진입합니다.
        /// </summary>
        /// <param name="guardStartStaminaCost">이번 가드 시작에 필요한 스테미나 비용입니다.</param>
        /// <param name="cancelGuardBreakAnimation">진행 중인 가드 브레이크 연출을 취소할지 여부입니다.</param>
        /// <param name="interruptHitStopOnStart">가드 진입 확정 시 활성 HitStop을 종료할지 여부입니다.</param>
        /// <returns>가드 시작에 성공하면 true입니다.</returns>
        private bool TryBeginGuardWithCost(
            long guardStartStaminaCost,
            bool cancelGuardBreakAnimation,
            bool interruptHitStopOnStart = false)
        {
            if (!TrySpendStamina(Math.Max(0L, guardStartStaminaCost)))
            {
                // 스테미나가 부족하면 Guard 진입 자체를 막고, 브레이크 중이면 해당 연출을 유지합니다.
                return false;
            }

            if (interruptHitStopOnStart)
            {
                // 가드 진입이 확정된 뒤 이전 상태와 속도를 복원하지 않고 HitStop을 종료해야
                // 정지된 Animator와 Rigidbody2D가 가드 시작 상태를 덮어쓰지 않습니다.
                actionCharacterBase.HitStopController.TerminateForExternalActionOverride();
            }

            // 새 가드 입력이 정상 진입하면 이전 CC 예약 상태는 더 이상 유효하지 않습니다.
            ClearControlUnlockGuardReservations();

            _requiresReleaseBeforeReGuard = false;
            _isAwaitingGuardBreakReGuard = false;

            // Tick 초기화
            _staminaTickElapsed = 0f;
            _guardStartedTime = Time.time;
            ClearGuardSuccessAnimationState();

            if (cancelGuardBreakAnimation)
            {
                ClearGuardBreakAnimationState();
            }

            // 이동 멈춤
            actionCharacterBase.directionNormalize = Vector3.zero;
            actionCharacterBase.Stop(true);

            // Start → Wait로 이어지는 구간
            if (_hasStart)
            {
                _phase = GuardPhase.Start;
                actionCharacterBase.CharacterAnimationController?.PlayCharacterAnimation(_animGuardStart);
            }
            else
            {
                // Start가 없으면 즉시 Wait로
                BeginWait();
            }

            return true;
        }

        /// <summary>
        /// 최대 스테미나 비율 기반 저스트 가드 성공 스테미나 소모량을 계산합니다.
        /// 너무 작은 비율 때문에 0으로 반올림되는 경우에는 최소 1을 반환합니다.
        /// </summary>
        /// <returns>비율 정책으로 계산된 스테미나 소모량입니다.</returns>
        private long ResolveJustGuardPercentStaminaCost()
        {
            if (actionCharacterBase == null) return 0;

            long maxStamina = actionCharacterBase.MaxStamina.Value;
            if (maxStamina <= 0) return 0;

            float ratio = Mathf.Clamp01(_justGuardSuccessStaminaCostValue);
            if (ratio <= 0f) return 0;

            long amount = Mathf.RoundToInt(maxStamina * ratio);
            return amount <= 0 ? 1 : amount;
        }

        /// <summary>
        /// Guard 버튼 Up(Released) 처리.
        /// CC처럼 조작 불가 상태에서 Release 입력이 들어오면 즉시 종료 애니메이션을 끼워 넣지 않고,
        /// 조작 가능 상태로 복귀한 뒤 가드 상태만 안전하게 정리하도록 예약합니다.
        /// </summary>
        public void GuardUp()
        {
            _isGuardInputHeld = false;

            // 가드 브레이크 이후 재가드 잠금은 Release 입력이 들어온 시점에 해제합니다.
            _requiresReleaseBeforeReGuard = false;

            if (!IsGuarding)
            {
                ClearControlUnlockGuardReservations();
                return;
            }

            // 브레이크 연출 중 Release가 들어오면 재가드 잠금만 해제하고, 브레이크 연출은 유지합니다.
            if (_phase == GuardPhase.Break)
            {
                _pendingResumeGuardWaitAfterControlUnlock = false;
                return;
            }

            if (actionCharacterBase != null && actionCharacterBase.IsDontControl())
            {
                _pendingReleaseAfterControlUnlock = true;
                _pendingResumeGuardWaitAfterControlUnlock = false;
                _staminaTickElapsed = 0f;
                ClearGuardSuccessAnimationState();
                return;
            }

            ClearControlUnlockGuardReservations();
            BeginEnd();
        }

        /// <summary>
        /// 조작 불가 상태에서 들어온 가드 해제 예약을 처리합니다.
        /// CC 진행 중에는 유지 비용과 성공 애니메이션 복귀 처리를 멈추고,
        /// CC가 끝난 뒤 종료 애니메이션 없이 가드 상태를 정리합니다.
        /// </summary>
        /// <returns>해제 예약을 대기 중이거나 처리했으면 <see langword="true"/>입니다.</returns>
        private bool TryProcessPendingReleaseAfterControlUnlock()
        {
            if (!_pendingReleaseAfterControlUnlock) return false;

            if (actionCharacterBase != null && actionCharacterBase.IsDontControl())
                return true;

            _pendingReleaseAfterControlUnlock = false;
            _pendingResumeGuardWaitAfterControlUnlock = false;
            CancelGuard(skipEndAnimation: true, isStop: _isCharacterStop);
            return true;
        }

        /// <summary>
        /// Guarded 결과로 적용된 CC가 끝난 뒤 guard_wait 복귀 또는 가드 종료를 처리합니다.
        /// Release 예약이 우선이므로, 이 함수는 Release 예약이 없는 경우에만 호출되어야 합니다.
        /// </summary>
        /// <returns>복귀 예약을 대기 중이거나 처리했으면 <see langword="true"/>입니다.</returns>
        private bool TryProcessPendingResumeGuardWaitAfterControlUnlock()
        {
            if (!_pendingResumeGuardWaitAfterControlUnlock) return false;

            // 가드 성공 애니메이션이 아직 재생 중이면 CC 종료 여부와 관계없이 복귀를 지연합니다.
            if (_isGuardSuccessAnimationPlaying)
                return true;

            if (actionCharacterBase != null && actionCharacterBase.IsDontControl())
                return true;

            _pendingResumeGuardWaitAfterControlUnlock = false;

            if (_isGuardInputHeld && IsActivelyGuarding)
            {
                BeginWait();
                return true;
            }

            CancelGuard(skipEndAnimation: true, isStop: _isCharacterStop);
            return true;
        }

        /// <summary>
        /// Guarded 결과에 CC가 포함된 경우, 조작 가능 상태로 돌아온 뒤 guard_wait 복귀를 예약합니다.
        /// 가드 키를 먼저 떼면 Release 예약이 우선 처리되어 가드 상태가 해제됩니다.
        /// </summary>
        /// <param name="crowdControlUid">Guarded 결과로 적용할 Crowd Control UID입니다.</param>
        private void RequestResumeGuardWaitAfterControlUnlock(int crowdControlUid)
        {
            if (crowdControlUid <= 0) return;
            if (!IsActivelyGuarding) return;

            _pendingResumeGuardWaitAfterControlUnlock = true;
        }

        /// <summary>
        /// CC 종료 후 가드 해제/복귀 예약 상태를 모두 초기화합니다.
        /// 새 가드 시작, 일반 종료, 브레이크 전환처럼 기존 예약이 더 이상 유효하지 않은 시점에 호출합니다.
        /// </summary>
        private void ClearControlUnlockGuardReservations()
        {
            _pendingReleaseAfterControlUnlock = false;
            _pendingResumeGuardWaitAfterControlUnlock = false;
        }

        private void BeginWait()
        {
            _phase = GuardPhase.Wait;

            // Wait 클립이 없으면 "정지 상태로 방어 유지"만 수행
            if (_hasWait)
            {
                actionCharacterBase.CharacterAnimationController?.PlayCharacterAnimation(_animGuardWait);
            }
        }

        private void BeginEnd(bool isStop = true)
        {
            if (_phase == GuardPhase.End) return;

            _phase = GuardPhase.End;
            _pendingResumeGuardWaitAfterControlUnlock = false;
            ClearGuardSuccessAnimationState();

            if (_hasEnd)
            {
                actionCharacterBase.CharacterAnimationController?.PlayCharacterAnimation(_animGuardEnd);
            }
            else
            {
                FinishGuard(isStop);
            }
        }

        private void OnAnimationEventGuardEnd(CharacterBase sender, EventArgsOnAnimationEventGuardEnd e)
        {
            FinishGuard();
        }

        private void FinishGuard(bool isStop = true)
        {
            _phase = GuardPhase.None;
            _staminaTickElapsed = 0f;
            _guardStartedTime = -999f;
            ClearControlUnlockGuardReservations();
            ClearGuardSuccessAnimationState();
            ClearGuardBreakAnimationState();
            ClearGuardBreakReGuardState();
            // 상태 복귀는 Stop이 담당(기존 설계 유지)
            if (isStop)
                actionCharacterBase?.Stop(true);
        }

        /// <summary>
        /// 가드 성공 애니메이션을 재생합니다.
        /// - 성공 애니메이션이 없으면 기존 동작을 유지합니다.
        /// - 가드 성공이 연속으로 들어오면 재생 시간을 갱신합니다.
        /// </summary>
        private void TryPlayGuardSuccessAnimation()
        {
            if (!_hasSuccess) return;
            if (!IsActivelyGuarding) return;

            _isGuardSuccessAnimationPlaying = true;
            _guardSuccessAnimationElapsed = 0f;
            actionCharacterBase.CharacterAnimationController?.PlayCharacterAnimation(_animGuardSuccess);
        }

        /// <summary>
        /// 성공 애니메이션 재생 시간을 추적하고, 종료 후 guard_wait 상태로 복귀합니다.
        /// </summary>
        private void TickGuardSuccessAnimation(float deltaTime)
        {
            if (!_isGuardSuccessAnimationPlaying) return;

            // 가드 상태가 아니면 성공 연출 상태를 즉시 정리합니다.
            if (!IsActivelyGuarding)
            {
                ClearGuardSuccessAnimationState();
                return;
            }

            _guardSuccessAnimationElapsed += Mathf.Max(0f, deltaTime);
            if (_guardSuccessAnimationElapsed < _guardSuccessDurationSeconds) return;

            ClearGuardSuccessAnimationState();

            if (_pendingResumeGuardWaitAfterControlUnlock)
                return;

            if (_isGuardInputHeld)
            {
                BeginWait();
            }
            else
            {
                BeginEnd(_isCharacterStop);
            }
        }

        /// <summary>
        /// 가드 성공 애니메이션 재생 상태를 초기화합니다.
        /// </summary>
        private void ClearGuardSuccessAnimationState()
        {
            _isGuardSuccessAnimationPlaying = false;
            _guardSuccessAnimationElapsed = 0f;
        }

        /// <summary>
        /// 가드 브레이크 애니메이션 재생 상태를 초기화합니다.
        /// </summary>
        private void ClearGuardBreakAnimationState()
        {
            _guardBreakAnimationElapsed = 0f;
            _guardBreakAnimationActiveDurationSeconds = 0f;
        }

        /// <summary>
        /// 가드 브레이크 중 재가드 전용 비용을 적용하기 위한 대기 상태를 초기화합니다.
        /// </summary>
        /// <remarks>
        /// 재가드 전용 비용은 guard_break 애니메이션을 중간에 끊고 다시 가드할 때만 사용해야 하므로,
        /// 브레이크 연출이 끝나 가드가 종료되면 일반 가드 시작 비용으로 돌아가야 합니다.
        /// </remarks>
        private void ClearGuardBreakReGuardState()
        {
            _isAwaitingGuardBreakReGuard = false;
        }

        /// <summary>
        /// 가드 브레이크 연출 시간을 추적하고, 종료 후 가드 상태를 완전히 해제합니다.
        /// </summary>
        /// <param name="deltaTime">프레임 경과 시간입니다.</param>
        private void TickGuardBreakAnimation(float deltaTime)
        {
            if (_phase != GuardPhase.Break) return;

            if (!_hasBreak || _guardBreakDurationSeconds <= 0f)
            {
                FinishGuard(_isCharacterStop);
                return;
            }

            float activeDuration = _guardBreakAnimationActiveDurationSeconds > 0f
                ? _guardBreakAnimationActiveDurationSeconds
                : _guardBreakDurationSeconds;

            _guardBreakAnimationElapsed += Mathf.Max(0f, deltaTime);
            if (_guardBreakAnimationElapsed < activeDuration) return;

            FinishGuard(_isCharacterStop);
        }

        /// <summary>
        /// 가드 성공 시점에 설정된 VFX를 단발로 재생합니다.
        /// </summary>
        /// <param name="isJustGuard">저스트 가드 성공 여부입니다.</param>
        private void TryPlayGuardSuccessVfx(bool isJustGuard)
        {
            if (actionCharacterBase == null) return;

            int vfxUid = _guardSuccessVfxUid;
            ConfigSortingLayer.Keys sortingLayer = _guardSuccessVfxSortingLayer;
            int sortingOrder = _guardSuccessVfxSortingOrder;
            Vector3 positionOffset = _guardSuccessVfxOffset;

            if (isJustGuard && _justGuardSuccessVfxUid > 0)
            {
                vfxUid = _justGuardSuccessVfxUid;
                sortingLayer = _justGuardSuccessVfxSortingLayer;
                sortingOrder = _justGuardSuccessVfxSortingOrder;
                positionOffset = _justGuardSuccessVfxOffset;
            }

            SceneGame scene = SceneGame.Instance;
            if (scene == null || scene.VfxManager == null) return;

            Vector2 visualDirection = ResolveGuardSuccessVfxDirection(actionCharacterBase);
            PlayGuardSuccessVfx(scene, vfxUid, sortingLayer, sortingOrder, positionOffset, visualDirection);

            if (isJustGuard)
                PlayAdditionalJustGuardSuccessVfx(scene, visualDirection);
        }

        /// <summary>
        /// 가드 성공 종류에 맞는 단발 효과음을 재생합니다.
        /// 저스트 가드 전용 UID가 없으면 일반 가드 성공 UID로 대체합니다.
        /// </summary>
        /// <param name="isJustGuard">저스트 가드 성공 여부입니다.</param>
        private void TryPlayGuardSuccessSound(bool isJustGuard)
        {
            int soundUid = isJustGuard && _justGuardSuccessSoundUid > 0
                ? _justGuardSuccessSoundUid
                : _guardSuccessSoundUid;
            if (soundUid <= 0)
                return;

            SceneGame scene = SceneGame.Instance;
            if (scene == null || scene.soundManager == null)
                return;

            scene.soundManager.PlayByUid(soundUid);
        }

        /// <summary>
        /// 가드 성공 VFX 1개를 현재 캐릭터 위치와 방향 기준으로 재생합니다.
        /// </summary>
        /// <param name="scene">VFX 매니저를 보유한 현재 게임 씬입니다.</param>
        /// <param name="vfxUid">재생할 vfx_effect 테이블 UID입니다. 0 이하면 재생하지 않습니다.</param>
        /// <param name="sortingLayer">VFX에 적용할 Sorting Layer입니다.</param>
        /// <param name="sortingOrder">VFX에 적용할 Sorting Order입니다.</param>
        /// <param name="positionOffset">캐릭터 위치 기준 VFX 오프셋입니다.</param>
        /// <param name="visualDirection">캐릭터 좌우 반전에 맞춘 VFX 방향입니다.</param>
        private void PlayGuardSuccessVfx(
            SceneGame scene,
            int vfxUid,
            ConfigSortingLayer.Keys sortingLayer,
            int sortingOrder,
            Vector3 positionOffset,
            Vector2 visualDirection)
        {
            if (vfxUid <= 0) return;
            if (scene == null || scene.VfxManager == null) return;
            if (actionCharacterBase == null) return;

            Vector3 mirroredOffset = ResolveGuardSuccessVfxOffsetByDirection(positionOffset, visualDirection);
            var spawnRequest = new VfxSpawnRequest
            {
                VfxUid = vfxUid,
                Owner = actionCharacterBase,
                Target = actionCharacterBase,
                WorldPosition = actionCharacterBase.transform.position,
                PositionOffset = mirroredOffset,
                SortingLayerOverride = sortingLayer,
                SortingOrderOverride = sortingOrder,
                ForceOneShot = true,
                // 가드 성공 VFX는 회전보다 좌우 반전 일치가 우선이므로, 방향 회전은 비활성화합니다.
                UseDirection = true,
                Direction = visualDirection,
                SourceDirection = visualDirection,
                DisableDirectionRotation = true,
            };

            scene.VfxManager.CreateVfx(spawnRequest);
        }

        /// <summary>
        /// 저스트 가드 성공 시 설정에 등록된 추가 VFX 목록을 순서대로 재생합니다.
        /// </summary>
        /// <param name="scene">VFX 매니저를 보유한 현재 게임 씬입니다.</param>
        /// <param name="visualDirection">캐릭터 좌우 반전에 맞춘 VFX 방향입니다.</param>
        private void PlayAdditionalJustGuardSuccessVfx(SceneGame scene, Vector2 visualDirection)
        {
            if (_additionalJustGuardSuccessVfxEntries == null || _additionalJustGuardSuccessVfxEntries.Count == 0)
                return;

            for (int i = 0; i < _additionalJustGuardSuccessVfxEntries.Count; i++)
            {
                GGemCoPlayerGuardSettings.GuardSuccessVfxEntry entry = _additionalJustGuardSuccessVfxEntries[i];
                if (entry == null)
                    continue;

                PlayGuardSuccessVfx(scene, entry.vfxUid, entry.sortingLayer, entry.sortingOrder, entry.offset, visualDirection);
            }
        }

        /// <summary>
        /// 가드 브레이크 시점에 설정된 VFX를 단발로 재생합니다.
        /// </summary>
        /// <param name="metadataDamage">가드 브레이크를 발생시킨 데미지 메타데이터입니다.</param>
        private void TryPlayGuardBreakVfx(MetadataDamage metadataDamage)
        {
            if (actionCharacterBase == null) return;

            int vfxUid = metadataDamage != null && metadataDamage.GuardBreakVfxUid > 0
                ? metadataDamage.GuardBreakVfxUid
                : _guardBreakVfxUid;
            if (vfxUid <= 0) return;

            SceneGame scene = SceneGame.Instance;
            if (scene == null || scene.VfxManager == null) return;

            Vector2 visualDirection = ResolveGuardSuccessVfxDirection(actionCharacterBase);
            Vector3 mirroredOffset = ResolveGuardSuccessVfxOffsetByDirection(_guardBreakVfxOffset, visualDirection);
            var spawnRequest = new VfxSpawnRequest
            {
                VfxUid = vfxUid,
                Owner = actionCharacterBase,
                Target = actionCharacterBase,
                WorldPosition = actionCharacterBase.transform.position,
                PositionOffset = mirroredOffset,
                SortingLayerOverride = _guardBreakVfxSortingLayer,
                SortingOrderOverride = _guardBreakVfxSortingOrder,
                ForceOneShot = true,
                UseDirection = true,
                Direction = visualDirection,
                SourceDirection = visualDirection,
                DisableDirectionRotation = true,
            };

            scene.VfxManager.CreateVfx(spawnRequest);
        }

        /// <summary>
        /// 가드 성공 VFX의 좌우 방향에 맞춰 X 오프셋을 보정합니다.
        /// 캐릭터가 좌측(Flip) 방향이면 입력된 X 오프셋 부호를 반전하여
        /// 오른쪽 기준으로 작성된 오프셋이 좌우 대칭으로 적용되도록 보장합니다.
        /// </summary>
        /// <param name="offset">설정에서 입력된 원본 위치 오프셋입니다.</param>
        /// <param name="visualDirection">VFX 좌우 반전에 사용되는 방향 벡터입니다.</param>
        /// <returns>좌우 방향 보정이 반영된 오프셋입니다.</returns>
        private static Vector3 ResolveGuardSuccessVfxOffsetByDirection(Vector3 offset, Vector2 visualDirection)
        {
            if (Mathf.Abs(offset.x) <= 0.0001f)
                return offset;

            if (Mathf.Abs(visualDirection.x) <= 0.0001f)
                return offset;

            if (visualDirection.x < 0f)
                offset.x = -offset.x;

            return offset;
        }

        /// <summary>
        /// 가드 성공 VFX에 적용할 좌우 방향을 계산합니다.
        /// 가능한 경우 <see cref="CharacterBase.CurrentFacing"/>을 우선 사용하고,
        /// X축 해석이 어려운 경우에는 캐릭터 Flip 상태를 기준으로 좌우 방향을 보정합니다.
        /// </summary>
        /// <param name="character">방향 기준이 되는 캐릭터입니다.</param>
        /// <returns>VFX 좌우 반전에 사용할 정규화된 수평 방향 벡터입니다.</returns>
        private static Vector2 ResolveGuardSuccessVfxDirection(CharacterBase character)
        {
            if (character == null)
                return Vector2.right;

            Vector2 facing = CharacterConstants.FacingToVector2(character.CurrentFacing);
            if (Mathf.Abs(facing.x) > 0.0001f)
                return facing.x > 0f ? Vector2.right : Vector2.left;

            return ResolveHorizontalDirectionByFlipState(character);
        }

        /// <summary>
        /// 캐릭터의 Flip 상태를 기준으로 좌우 수평 방향을 계산합니다.
        /// 기본 스프라이트 방향(<see cref="CharacterBase.defaultFacingDirection8"/>)을 함께 고려하여
        /// IsFlipped 값이 의미하는 실제 월드 좌우를 안정적으로 복원합니다.
        /// </summary>
        /// <param name="character">방향 기준 캐릭터입니다.</param>
        /// <returns>오른쪽 또는 왼쪽 단위 벡터입니다.</returns>
        private static Vector2 ResolveHorizontalDirectionByFlipState(CharacterBase character)
        {
            if (character == null)
                return Vector2.right;

            bool isFlipped = character.IsFlipped();
            return character.defaultFacingDirection8 switch
            {
                CharacterConstants.FacingDirection8.Left => isFlipped ? Vector2.right : Vector2.left,
                CharacterConstants.FacingDirection8.Right => isFlipped ? Vector2.left : Vector2.right,
                _ => character.transform.localScale.x < 0f ? Vector2.left : Vector2.right,
            };
        }

        /// <summary>
        /// 들어오는 피격 메타데이터를 기준으로 가드, 저스트 가드, 가드 브레이크 판정을 수행합니다.
        /// </summary>
        /// <param name="metadataDamage">공격 타입과 데미지 정보를 포함한 피격 메타데이터입니다.</param>
        /// <param name="result">가드 판정 결과와 추가 CC 정보를 반환합니다.</param>
        /// <returns>가드 시스템이 이번 피격을 처리했으면 <see langword="true"/>입니다.</returns>
        public bool TryResolveIncomingHit(MetadataDamage metadataDamage, out GuardResolutionResult result)
        {
            result = default;

            if (!IsActivelyGuarding) return false;
            if (actionCharacterBase == null) return false;
            if (metadataDamage == null) return false;
            if (metadataDamage.damage <= 0) return false;
            if (metadataDamage.GuardInteractionMode == GuardInteractionMode.IgnoreGuard) return false;
            if (_guardFrontOnly && !IsIncomingAttackFromFront(metadataDamage.attacker)) return false;

            bool isJustGuard = IsInJustGuardWindow(Time.time);
            if (TryResolveByAttackTypeRule(metadataDamage, isJustGuard, out result))
                return result.IsResolved;

            if (ShouldBreakGuard(metadataDamage, isJustGuard))
            {
                BeginGuardBreak(metadataDamage, crowdControlUid: 0);
                result = CreateGuardBreakResult(metadataDamage, crowdControlUid: 0);
                return true;
            }

            bool treatJustGuardAsNormalGuard =
                metadataDamage.GuardInteractionMode == GuardInteractionMode.BreakGuard &&
                isJustGuard &&
                metadataDamage.GuardBreakJustGuardPolicy == GuardBreakJustGuardPolicy.TreatAsNormalGuard;
            bool resolvedAsJustGuard = isJustGuard && !treatJustGuardAsNormalGuard;

            if (!OnGuardSuccess(resolvedAsJustGuard))
            {
                return false;
            }

            result = CreateGuardSuccessResult(metadataDamage, resolvedAsJustGuard, crowdControlUid: 0);
            NotifyGuardSuccessFeedback(metadataDamage, result);
            return true;
        }

        /// <summary>
        /// <see cref="GGemCoPlayerGuardSettings"/>에 등록된 공격 방어 타입 규칙으로 가드 결과를 계산합니다.
        /// </summary>
        /// <param name="metadataDamage">공격 타입과 데미지 정보를 포함한 피격 메타데이터입니다.</param>
        /// <param name="isJustGuard">현재 입력 타이밍이 저스트 가드 구간인지 여부입니다.</param>
        /// <param name="result">가드 판정 결과입니다.</param>
        /// <returns>타입 규칙으로 결과를 확정했으면 <see langword="true"/>입니다.</returns>
        private bool TryResolveByAttackTypeRule(MetadataDamage metadataDamage, bool isJustGuard, out GuardResolutionResult result)
        {
            result = default;

            if (playerGuardSettings == null)
                return false;
            if (!playerGuardSettings.TryGetGuardAttackTypeRule(metadataDamage.GuardAttackType, out var rule) || rule == null)
                return false;

            GuardResolutionOutcome outcome = rule.ResolveOutcome(isJustGuard);
            int crowdControlUid = rule.ResolveCrowdControlUid(outcome);

            switch (outcome)
            {
                case GuardResolutionOutcome.None:
                    return true;

                case GuardResolutionOutcome.GuardBroken:
                    BeginGuardBreak(metadataDamage, crowdControlUid);
                    result = CreateGuardBreakResult(metadataDamage, crowdControlUid);
                    return true;

                case GuardResolutionOutcome.JustGuarded:
                    if (!OnGuardSuccess(true))
                        return false;
                    result = CreateGuardSuccessResult(metadataDamage, true, crowdControlUid);
                    NotifyGuardSuccessFeedback(metadataDamage, result);
                    return true;

                case GuardResolutionOutcome.Guarded:
                    if (!OnGuardSuccess(false))
                        return false;
                    RequestResumeGuardWaitAfterControlUnlock(crowdControlUid);
                    result = CreateGuardSuccessResult(metadataDamage, false, crowdControlUid);
                    NotifyGuardSuccessFeedback(metadataDamage, result);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 일반 가드 또는 저스트 가드 성공 결과를 생성합니다.
        /// </summary>
        /// <param name="metadataDamage">원본 피격 메타데이터입니다.</param>
        /// <param name="resolvedAsJustGuard">저스트 가드로 처리되었는지 여부입니다.</param>
        /// <param name="crowdControlUid">가드 결과로 추가 적용할 Crowd Control UID입니다.</param>
        /// <returns>가드 성공 판정 결과입니다.</returns>
        private GuardResolutionResult CreateGuardSuccessResult(MetadataDamage metadataDamage, bool resolvedAsJustGuard, int crowdControlUid)
        {
            float damageMultiplier = resolvedAsJustGuard ? _justGuardDamageMultiplier : _guardDamageMultiplier;
            bool suppressHitReaction = resolvedAsJustGuard ? _justGuardSuppressHitReaction : _guardSuppressHitReaction;
            long remainingDamage = CalculateReducedDamage(metadataDamage.damage, damageMultiplier);

            var result = new GuardResolutionResult
            {
                IsResolved = true,
                IsJustGuard = resolvedAsJustGuard,
                Outcome = resolvedAsJustGuard ? GuardResolutionOutcome.JustGuarded : GuardResolutionOutcome.Guarded,
                RemainingDamage = remainingDamage,
                SuppressHitReaction = suppressHitReaction,
                CrowdControlUid = crowdControlUid,
            };

            ApplyGuardDebugFeedback(
                ref result,
                resolvedAsJustGuard ? "JUST GUARD" : "GUARD",
                resolvedAsJustGuard ? Color.yellow : Color.cyan,
                resolvedAsJustGuard ? _justGuardDebugFeedbackSprite : _guardDebugFeedbackSprite,
                ResolveGuardDebugFeedbackUiEffectUid(resolvedAsJustGuard));

            return result;
        }

        /// <summary>
        /// 가드 브레이크 판정 결과를 생성합니다.
        /// </summary>
        /// <param name="metadataDamage">원본 피격 메타데이터입니다.</param>
        /// <param name="crowdControlUid">가드 브레이크 결과로 추가 적용할 Crowd Control UID입니다.</param>
        /// <returns>가드 브레이크 판정 결과입니다.</returns>
        private GuardResolutionResult CreateGuardBreakResult(MetadataDamage metadataDamage, int crowdControlUid)
        {
            var result = new GuardResolutionResult
            {
                IsResolved = true,
                IsJustGuard = false,
                Outcome = GuardResolutionOutcome.GuardBroken,
                RemainingDamage = CalculateReducedDamage(metadataDamage.damage, ResolveGuardBreakDamageMultiplier(metadataDamage)),
                SuppressHitReaction = true,
                CrowdControlUid = crowdControlUid,
                CrowdControlAnimationOverride = BuildGuardBreakCrowdControlAnimationOverride(crowdControlUid),
            };

            ApplyGuardDebugFeedback(
                ref result,
                ResolveGuardBreakFeedbackText(metadataDamage),
                Color.red,
                _guardBreakDebugFeedbackSprite,
                _guardBreakDebugFeedbackUiEffectUid);

            return result;
        }

        /// <summary>
        /// 가드 디버그 피드백 설정에 따라 판정 결과에 텍스트 또는 스프라이트 표시 데이터를 채웁니다.
        /// </summary>
        /// <param name="result">표시 데이터를 반영할 가드 판정 결과입니다.</param>
        /// <param name="fallbackText">텍스트 표시 또는 스프라이트 누락 시 사용할 기본 문구입니다.</param>
        /// <param name="feedbackColor">텍스트와 스프라이트에 적용할 표시 색상입니다.</param>
        /// <param name="feedbackSprite">스프라이트 표시 모드에서 사용할 이미지입니다.</param>
        /// <param name="uiEffectUid">생성된 피드백 UI에 재생할 ui_effect 데이터 테이블 UID입니다.</param>
        private void ApplyGuardDebugFeedback(
            ref GuardResolutionResult result,
            string fallbackText,
            Color feedbackColor,
            Sprite feedbackSprite,
            int uiEffectUid)
        {
            result.FeedbackText = string.Empty;
            result.FeedbackColor = feedbackColor;
            result.FeedbackSprite = null;
            result.FeedbackSpriteSize = Vector2.zero;
            result.UseDefenderXForFeedback = false;
            result.FeedbackDefenderXOffset = 0f;
            result.OverrideFeedbackRandomXRange = false;
            result.FeedbackRandomXRange = 0f;
            result.FeedbackUiEffectUid = Mathf.Max(0, uiEffectUid);

            if (playerGuardSettings == null || !playerGuardSettings.showGuardFeedback)
                return;

            ApplyGuardDebugFeedbackPositionPolicy(ref result);

            if (_guardDebugFeedbackDisplayMode == GuardDebugFeedbackDisplayMode.Sprite && feedbackSprite != null)
            {
                result.FeedbackSprite = feedbackSprite;
                result.FeedbackSpriteSize = _guardDebugFeedbackSpriteSize;
                return;
            }

            // 스프라이트 모드에서 이미지가 비어 있으면 기존 텍스트 피드백으로 안전하게 폴백합니다.
            result.FeedbackText = fallbackText;
        }

        /// <summary>
        /// 가드 디버그 피드백의 X 좌표 기준 정책을 판정 결과에 반영합니다.
        /// </summary>
        /// <param name="result">위치 정책을 반영할 가드 판정 결과입니다.</param>
        private void ApplyGuardDebugFeedbackPositionPolicy(ref GuardResolutionResult result)
        {
            if (_guardDebugFeedbackXAxisPolicy != GuardDebugFeedbackXAxisPolicy.PlayerXWithOffset)
                return;

            result.UseDefenderXForFeedback = true;
            result.FeedbackDefenderXOffset = _guardDebugFeedbackPlayerXOffset;
            // 플레이어 X 기준 고정 정책에서는 플로팅 텍스트 기본 랜덤 X 흔들림을 꺼야 기준점이 흔들리지 않습니다.
            result.OverrideFeedbackRandomXRange = true;
            result.FeedbackRandomXRange = 0f;
        }

        /// <summary>
        /// 가드 결과 종류에 맞는 UI 효과 UID를 반환합니다.
        /// </summary>
        /// <param name="resolvedAsJustGuard">저스트 가드 성공으로 처리되었는지 여부입니다.</param>
        /// <returns>적용할 ui_effect 데이터 테이블 UID입니다. 없으면 0입니다.</returns>
        private int ResolveGuardDebugFeedbackUiEffectUid(bool resolvedAsJustGuard)
        {
            if (resolvedAsJustGuard && _justGuardDebugFeedbackUiEffectUid > 0)
            {
                return _justGuardDebugFeedbackUiEffectUid;
            }

            return _guardDebugFeedbackUiEffectUid;
        }

        /// <summary>
        /// 현재 공격 메타데이터와 저스트 가드 판정 결과를 기준으로 가드 브레이크를 실행해야 하는지 확인합니다.
        /// </summary>
        /// <param name="metadataDamage">공격 메타데이터입니다.</param>
        /// <param name="isJustGuard">현재 입력 타이밍이 저스트 가드 구간인지 여부입니다.</param>
        /// <returns>가드 브레이크를 실행해야 하면 <see langword="true"/>입니다.</returns>
        private static bool ShouldBreakGuard(MetadataDamage metadataDamage, bool isJustGuard)
        {
            if (metadataDamage == null) return false;
            if (metadataDamage.GuardInteractionMode != GuardInteractionMode.BreakGuard) return false;
            if (!isJustGuard) return true;

            return metadataDamage.GuardBreakJustGuardPolicy == GuardBreakJustGuardPolicy.BreakEvenJustGuard;
        }

        /// <summary>
        /// 가드 브레이크 상태로 전환하고, 가드 유지/성공 연출을 정리합니다.
        /// </summary>
        /// <param name="metadataDamage">가드 브레이크를 발생시킨 데미지 메타데이터입니다.</param>
        /// <param name="crowdControlUid">가드 브레이크 결과로 적용할 Crowd Control UID입니다.</param>
        private void BeginGuardBreak(MetadataDamage metadataDamage, int crowdControlUid)
        {
            _phase = GuardPhase.Break;
            _requiresReleaseBeforeReGuard = true;
            _isAwaitingGuardBreakReGuard = true;
            ClearControlUnlockGuardReservations();
            _staminaTickElapsed = 0f;
            _guardStartedTime = -999f;
            ClearGuardSuccessAnimationState();
            ClearGuardBreakAnimationState();

            long staminaCost = metadataDamage != null && metadataDamage.GuardBreakStaminaCost > 0
                ? metadataDamage.GuardBreakStaminaCost
                : _guardBreakStaminaCost;
            TrySpendStamina(staminaCost);

            if (actionCharacterBase != null)
            {
                actionCharacterBase.directionNormalize = Vector3.zero;
                actionCharacterBase.Stop(true);
            }

            TryPlayGuardBreakVfx(metadataDamage);

            if (!_hasBreak)
            {
                FinishGuard(_isCharacterStop);
                return;
            }

            CrowdControlAnimationOverride animationOverride = BuildGuardBreakCrowdControlAnimationOverride(crowdControlUid);
            if (animationOverride.IsValid)
            {
                _guardBreakAnimationActiveDurationSeconds = animationOverride.ResolvePlaybackDuration(_guardBreakDurationSeconds);
                return;
            }

            _guardBreakAnimationActiveDurationSeconds = _guardBreakDurationSeconds;
            actionCharacterBase.CharacterAnimationController?.PlayCharacterAnimation(_animGuardBreak);
        }

        /// <summary>
        /// 가드 브레이크 결과 CC에 전달할 애니메이션 오버라이드 데이터를 생성합니다.
        /// </summary>
        /// <param name="crowdControlUid">가드 브레이크 결과로 적용할 Crowd Control UID입니다.</param>
        /// <returns>유효한 CC와 설정이 있으면 guard_break 애니메이션 오버라이드입니다.</returns>
        private CrowdControlAnimationOverride BuildGuardBreakCrowdControlAnimationOverride(int crowdControlUid)
        {
            if (!_syncGuardBreakAnimationToCrowdControl)
                return default;
            if (crowdControlUid <= 0)
                return default;
            if (!_hasBreak || string.IsNullOrWhiteSpace(_animGuardBreak))
                return default;

            CrowdControlRuntimeData crowdControl = ResolveCrowdControlRuntimeData(crowdControlUid);
            if (crowdControl == null)
                return default;
            if (crowdControl.Duration <= 0f)
                return default;

            return new CrowdControlAnimationOverride
            {
                UseInitialAnimationOverride = true,
                InitialAnimationName = _animGuardBreak,
                Loop = false,
                ForceReset = true,
                FitToTargetDurationWhenLonger = _onlySpeedUpGuardBreakAnimationWhenLonger,
                TargetDurationSeconds = crowdControl.Duration,
                MinTargetDurationSeconds = _guardBreakAnimationMinTargetDuration,
                MaxTimeScale = _guardBreakAnimationMaxTimeScale,
                UseEasing = _applyCrowdControlEasingToGuardBreakAnimation,
                EaseType = crowdControl.EaseType,
                SuppressRuntimePhaseAnimations = true,
            };
        }

        /// <summary>
        /// Crowd Control UID로 런타임 데이터를 조회합니다.
        /// </summary>
        /// <param name="crowdControlUid">조회할 Crowd Control UID입니다.</param>
        /// <returns>조회된 런타임 데이터입니다. 없으면 null입니다.</returns>
        private static CrowdControlRuntimeData ResolveCrowdControlRuntimeData(int crowdControlUid)
        {
            return TableLoaderManager.Instance != null
                ? TableLoaderManager.Instance.GetCrowdControlRuntimeData(crowdControlUid, logIfMissing: false)
                : null;
        }

        /// <summary>
        /// 가드 브레이크 시 실제 HP에 적용할 데미지 배율을 계산합니다.
        /// </summary>
        /// <param name="metadataDamage">공격 메타데이터입니다.</param>
        /// <returns>0~1 범위로 보정된 데미지 배율입니다.</returns>
        private float ResolveGuardBreakDamageMultiplier(MetadataDamage metadataDamage)
        {
            if (metadataDamage == null) return _guardBreakDamageMultiplier;
            return Mathf.Clamp01(metadataDamage.GuardBreakDamageMultiplier);
        }

        /// <summary>
        /// 가드 브레이크 시 표시할 피드백 텍스트를 계산합니다.
        /// </summary>
        /// <param name="metadataDamage">공격 메타데이터입니다.</param>
        /// <returns>화면에 표시할 가드 브레이크 텍스트입니다.</returns>
        private string ResolveGuardBreakFeedbackText(MetadataDamage metadataDamage)
        {
            if (metadataDamage != null && !string.IsNullOrWhiteSpace(metadataDamage.GuardBreakFeedbackText))
                return metadataDamage.GuardBreakFeedbackText;

            return string.IsNullOrWhiteSpace(_guardBreakFeedbackText) ? "GUARD BREAK" : _guardBreakFeedbackText;
        }

        private bool IsInJustGuardWindow(float now)
        {
            if (!_enableJustGuard) return false;
            if (!IsActivelyGuarding) return false;
            if (_justGuardWindowDuration <= 0f) return false;

            float elapsed = now - _guardStartedTime;
            if (elapsed < 0f) return false;
            if (elapsed < _justGuardOpenDelay) return false;
            return elapsed <= (_justGuardOpenDelay + _justGuardWindowDuration);
        }

        private bool IsIncomingAttackFromFront(GameObject attacker)
        {
            if (actionCharacterBase == null) return false;
            if (attacker == null) return true;

            float deltaX = attacker.transform.position.x - actionCharacterBase.transform.position.x;
            if (Mathf.Abs(deltaX) <= 0.0001f)
            {
                return true;
            }

            return actionCharacterBase.CurrentFacing switch
            {
                CharacterConstants.FacingDirection8.Left => deltaX <= 0f,
                CharacterConstants.FacingDirection8.Right => deltaX >= 0f,
                CharacterConstants.FacingDirection8.UpLeft => deltaX <= 0f,
                CharacterConstants.FacingDirection8.DownLeft => deltaX <= 0f,
                CharacterConstants.FacingDirection8.UpRight => deltaX >= 0f,
                CharacterConstants.FacingDirection8.DownRight => deltaX >= 0f,
                _ => true,
            };
        }

        private static long CalculateReducedDamage(long damage, float multiplier)
        {
            if (damage <= 0) return 0;
            if (multiplier <= 0f) return 0;
            if (multiplier >= 1f) return damage;

            return Math.Max(0L, (long)Mathf.Ceil(damage * multiplier));
        }

        public void CancelGuard(bool skipEndAnimation = false, bool isStop = true)
        {
            if (skipEndAnimation || !_hasEnd)
            {
                FinishGuard(isStop);
                return;
            }

            BeginEnd(isStop);
        }

        private bool TrySpendStamina(long amount)
        {
            if (actionCharacterBase == null) return false;

            if (amount <= 0) return true;

            return actionCharacterBase.TrySpendStamina(amount);
        }
    }
}
