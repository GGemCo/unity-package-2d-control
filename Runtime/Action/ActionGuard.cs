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
        private int _justGuardSuccessVfxUid;
        private ConfigSortingLayer.Keys _justGuardSuccessVfxSortingLayer;
        private int _justGuardSuccessVfxSortingOrder;
        private Vector3 _justGuardSuccessVfxOffset;
        private long _guardBreakStaminaCost;
        private float _guardBreakDamageMultiplier;
        private string _guardBreakFeedbackText;
        private int _guardBreakVfxUid;
        private ConfigSortingLayer.Keys _guardBreakVfxSortingLayer;
        private int _guardBreakVfxSortingOrder;
        private Vector3 _guardBreakVfxOffset;

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
        private bool _requiresReleaseBeforeReGuard;

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
            _justGuardSuccessVfxUid = playerActionSettings.justGuardSuccessVfxUid;
            _justGuardSuccessVfxSortingLayer = playerActionSettings.justGuardSuccessVfxSortingLayer;
            _justGuardSuccessVfxSortingOrder = playerActionSettings.justGuardSuccessVfxSortingOrder;
            _justGuardSuccessVfxOffset = playerActionSettings.justGuardSuccessVfxOffset;
            _guardBreakStaminaCost = playerActionSettings.guardBreakStaminaCost;
            _guardBreakDamageMultiplier = Mathf.Clamp01(playerActionSettings.guardBreakDamageMultiplier);
            _guardBreakFeedbackText = string.IsNullOrWhiteSpace(playerActionSettings.guardBreakFeedbackText)
                ? "GUARD BREAK"
                : playerActionSettings.guardBreakFeedbackText;
            _guardBreakVfxUid = playerActionSettings.guardBreakVfxUid;
            _guardBreakVfxSortingLayer = playerActionSettings.guardBreakVfxSortingLayer;
            _guardBreakVfxSortingOrder = playerActionSettings.guardBreakVfxSortingOrder;
            _guardBreakVfxOffset = playerActionSettings.guardBreakVfxOffset;

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

            // 가드 브레이크 이후에는 사용자가 가드 키를 한 번 뗀 뒤 다시 눌러야 합니다.
            if (_requiresReleaseBeforeReGuard) return;

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
            TryPlayGuardSuccessVfx(isJustGuard);
            return true;
        }

        /// <summary>
        /// Guard 버튼 Up(Released) 처리.
        /// </summary>
        public void GuardUp()
        {
            // 가드 브레이크 이후 재가드 잠금은 Release 입력이 들어온 시점에 해제합니다.
            _requiresReleaseBeforeReGuard = false;

            if (!IsGuarding) return;

            // 브레이크 연출 중 Release가 들어오면 재가드 잠금만 해제하고, 브레이크 연출은 유지합니다.
            if (_phase == GuardPhase.Break) return;

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
            ClearGuardBreakAnimationState();
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
        /// 가드 브레이크 애니메이션 재생 상태를 초기화합니다.
        /// </summary>
        private void ClearGuardBreakAnimationState()
        {
            _guardBreakAnimationElapsed = 0f;
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

            _guardBreakAnimationElapsed += Mathf.Max(0f, deltaTime);
            if (_guardBreakAnimationElapsed < _guardBreakDurationSeconds) return;

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

            if (vfxUid <= 0) return;

            SceneGame scene = SceneGame.Instance;
            if (scene == null || scene.VfxManager == null) return;

            Vector2 visualDirection = ResolveGuardSuccessVfxDirection(actionCharacterBase);
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

            if (ShouldBreakGuard(metadataDamage, isJustGuard))
            {
                BeginGuardBreak(metadataDamage);
                result = new GuardResolutionResult
                {
                    IsResolved = true,
                    IsJustGuard = false,
                    Outcome = GuardResolutionOutcome.GuardBroken,
                    RemainingDamage = CalculateReducedDamage(metadataDamage.damage, ResolveGuardBreakDamageMultiplier(metadataDamage)),
                    SuppressHitReaction = true,
                    FeedbackText = ResolveGuardBreakFeedbackText(metadataDamage),
                    FeedbackColor = Color.red,
                };

                if (!playerActionSettings.showGuardDebugText)
                    result.FeedbackText = string.Empty;
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

            float damageMultiplier = resolvedAsJustGuard ? _justGuardDamageMultiplier : _guardDamageMultiplier;
            bool suppressHitReaction = resolvedAsJustGuard ? _justGuardSuppressHitReaction : _guardSuppressHitReaction;

            long remainingDamage = CalculateReducedDamage(metadataDamage.damage, damageMultiplier);

            result = new GuardResolutionResult
            {
                IsResolved = true,
                IsJustGuard = resolvedAsJustGuard,
                Outcome = resolvedAsJustGuard ? GuardResolutionOutcome.JustGuarded : GuardResolutionOutcome.Guarded,
                RemainingDamage = remainingDamage,
                SuppressHitReaction = suppressHitReaction,
                FeedbackText = resolvedAsJustGuard ? "JUST GUARD" : "GUARD",
                FeedbackColor = resolvedAsJustGuard ? Color.yellow : Color.cyan,
            };
            if (!playerActionSettings.showGuardDebugText)
                result.FeedbackText = string.Empty;
            return true;
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
        private void BeginGuardBreak(MetadataDamage metadataDamage)
        {
            _phase = GuardPhase.Break;
            _requiresReleaseBeforeReGuard = true;
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

            if (_hasBreak)
            {
                actionCharacterBase.CharacterAnimationController?.PlayCharacterAnimation(_animGuardBreak);
            }
            else
            {
                FinishGuard(_isCharacterStop);
            }
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
