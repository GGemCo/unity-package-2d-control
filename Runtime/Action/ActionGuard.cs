using System;
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

        private enum GuardPhase
        {
            None,
            Start,
            Wait,
            End,
        }

        private GuardPhase _phase;

        private string _animGuardStart;
        private string _animGuardWait;
        private string _animGuardEnd;
        private string _animGuardSuccess;

        // --- 애니메이션 존재 여부 캐시 ---
        private bool _hasStart, _hasWait, _hasEnd, _hasSuccess;
        private float _guardSuccessDurationSeconds;
        private bool _isGuardSuccessAnimationPlaying;
        private float _guardSuccessAnimationElapsed;
        
        // [Tooltip("방어 시작시 차감되는 스테미나")]
        private long _guardStartStaminaCost;

        // [Tooltip("방어 성공시 차감되는 스테미나")]
        private long _guardSuccessStaminaCost;

        // [Tooltip("가드를 하는 중이면, 몇 초 마다 차감할 것인지")]
        private float _guardStaminaTickInterval;

        // [Tooltip("guardStaminaTickInterval 시간마다 얼마나 차감할 것인지")]
        private long _guardStaminaTickCost;
        private int _guardSuccessVfxUid;
        private ConfigSortingLayer.Keys _guardSuccessVfxSortingLayer;
        private int _guardSuccessVfxSortingOrder;
        private Vector3 _guardSuccessVfxOffset;

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

        private float _guardStartedTime = -999f;
        private bool _isCharacterStop;

        public override void Initialize(InputManager inputManager, CharacterBase characterBase, CharacterBaseController characterBaseController)
        {
            // ApplySettings에서 playerActionSettings 사용
            base.Initialize(inputManager, characterBase, characterBaseController);
            actionCharacterBase.OnAnimationEventGuardEnd += OnAnimationEventGuardEnd;
            // 탈진 시스템을 사용하면, CharacterBase.Stop 처리를 하지 않는다.
            _isCharacterStop = !(playerActionSettings && playerActionSettings.enableExhaustion);
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
            var prefix = playerActionSettings != null && !string.IsNullOrWhiteSpace(playerActionSettings.prefixGuardAnimation)
                ? playerActionSettings.prefixGuardAnimation
                : "guard";

            _animGuardStart = prefix;
            _animGuardWait = prefix + "_wait";
            _animGuardEnd = prefix + "_end";
            _animGuardSuccess = prefix + "_success";
            _hasStart = HasAnimation(_animGuardStart);
            _hasWait = HasAnimation(_animGuardWait);
            _hasEnd = HasAnimation(_animGuardEnd);
            _hasSuccess = HasAnimation(_animGuardSuccess);

            _guardSuccessDurationSeconds = 0f;
            if (_hasSuccess && actionCharacterBase?.CharacterAnimationController != null)
            {
                _guardSuccessDurationSeconds = Mathf.Max(
                    0f,
                    actionCharacterBase.CharacterAnimationController.GetCharacterAnimationDuration(_animGuardSuccess, false));
            }
        }

        private void InitializeGuardSettings()
        {
            if (!playerActionSettings) return;
            _guardStartStaminaCost = playerActionSettings.guardStartStaminaCost;
            _guardSuccessStaminaCost = playerActionSettings.guardSuccessStaminaCost;
            _guardStaminaTickInterval = playerActionSettings.guardStaminaTickInterval;
            _guardStaminaTickCost = playerActionSettings.guardStaminaTickCost;
            _guardSuccessVfxUid = playerActionSettings.guardSuccessVfxUid;
            _guardSuccessVfxSortingLayer = playerActionSettings.guardSuccessVfxSortingLayer;
            _guardSuccessVfxSortingOrder = playerActionSettings.guardSuccessVfxSortingOrder;
            _guardSuccessVfxOffset = playerActionSettings.guardSuccessVfxOffset;

            _enableJustGuard = playerActionSettings.enableJustGuard;
            _justGuardOpenDelay = Mathf.Max(0f, playerActionSettings.justGuardOpenDelay);
            _justGuardWindowDuration = Mathf.Max(0f, playerActionSettings.justGuardWindowDuration);
            _guardDamageMultiplier = Mathf.Clamp01(playerActionSettings.guardDamageMultiplier);
            _justGuardDamageMultiplier = Mathf.Clamp01(playerActionSettings.justGuardDamageMultiplier);
            _guardFrontOnly = playerActionSettings.guardFrontOnly;
            _guardSuppressHitReaction = playerActionSettings.guardSuppressHitReaction;
            _justGuardSuppressHitReaction = playerActionSettings.justGuardSuppressHitReaction;
        }

        /// <summary>
        /// Guard 버튼 Down(Pressed) 처리.
        /// </summary>
        public void GuardDown()
        {
            if (actionCharacterBase == null) return;
            if (actionCharacterBase.IsStatusDead()) return;

            // 이미 가드 중이면 유지 (중복 호출 방지)
            if (IsGuarding) return;

            // Guard 시작 비용 지불(부족하면 진입 불가)
            if (!TrySpendStamina(_guardStartStaminaCost))
            {
                // 스테미나가 부족하면 Guard 진입 자체를 막는다.
                return;
            }

            // Tick 초기화
            _staminaTickElapsed = 0f;
            _guardStartedTime = Time.time;
            ClearGuardSuccessAnimationState();

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

            // 스테미나 0이면 입력과 무관하게 즉시 해제
            if (actionCharacterBase.CurrentStamina.Value <= 0)
            {
                CancelGuard(true, _isCharacterStop);
                return;
            }

            TickGuardSuccessAnimation(deltaTime);

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
        public bool OnGuardSuccess()
        {
            if (!IsGuarding) return false;
            if (actionCharacterBase == null) return false;

            if (!TrySpendStamina(_guardSuccessStaminaCost))
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
            TryPlayGuardSuccessVfx();
            return true;
        }

        /// <summary>
        /// Guard 버튼 Up(Released) 처리.
        /// </summary>
        public void GuardUp()
        {
            if (!IsGuarding) return;

            BeginEnd();
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
            ClearGuardSuccessAnimationState();
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
            BeginWait();
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
        /// 가드 성공 시점에 설정된 VFX를 단발로 재생합니다.
        /// </summary>
        private void TryPlayGuardSuccessVfx()
        {
            if (_guardSuccessVfxUid <= 0) return;
            if (actionCharacterBase == null) return;

            SceneGame scene = SceneGame.Instance;
            if (scene == null || scene.VfxManager == null) return;

            var spawnRequest = new VfxSpawnRequest
            {
                VfxUid = _guardSuccessVfxUid,
                Owner = actionCharacterBase,
                Target = actionCharacterBase,
                WorldPosition = actionCharacterBase.transform.position,
                PositionOffset = _guardSuccessVfxOffset,
                SortingLayerOverride = _guardSuccessVfxSortingLayer,
                SortingOrderOverride = _guardSuccessVfxSortingOrder,
                ForceOneShot = true,
            };

            scene.VfxManager.CreateVfx(spawnRequest);
        }

        public bool TryResolveIncomingHit(MetadataDamage metadataDamage, out GuardResolutionResult result)
        {
            result = default;

            if (!IsActivelyGuarding) return false;
            if (actionCharacterBase == null) return false;
            if (metadataDamage == null) return false;
            if (metadataDamage.damage <= 0) return false;
            if (_guardFrontOnly && !IsIncomingAttackFromFront(metadataDamage.attacker)) return false;

            bool isJustGuard = IsInJustGuardWindow(Time.time);
            if (!OnGuardSuccess())
            {
                return false;
            }

            float damageMultiplier = isJustGuard ? _justGuardDamageMultiplier : _guardDamageMultiplier;
            bool suppressHitReaction = isJustGuard ? _justGuardSuppressHitReaction : _guardSuppressHitReaction;

            long remainingDamage = CalculateReducedDamage(metadataDamage.damage, damageMultiplier);

            result = new GuardResolutionResult
            {
                IsResolved = true,
                IsJustGuard = isJustGuard,
                RemainingDamage = remainingDamage,
                SuppressHitReaction = suppressHitReaction,
                FeedbackText = isJustGuard ? "JUST GUARD" : "GUARD",
                FeedbackColor = isJustGuard ? Color.yellow : Color.cyan,
            };
            if (!playerActionSettings.showGuardDebugText)
                result.FeedbackText = string.Empty;
            return true;
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
