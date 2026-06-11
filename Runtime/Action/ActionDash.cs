using System.Collections.Generic;
using System;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 대시 액션 (설정: 이동 거리, 지속 시간, 이징)
    /// - s(t) 이징 → Δs * distance를 프레임별 누적, 총 이동거리 정확 보장
    /// - AnimationCurve 주입 시 커브 우선, 없으면 GGemCo2DCore.Easing 사용
    /// - 애니메이션 폴백, 이벤트 워치독, 충돌 예측(Cast)로 조기 종료
    /// </summary>
    public class ActionDash : ActionBase
    {
        // (선택) 외부에서 상태 조회용 프로퍼티
        public bool IsDashing => _phase != DashPhase.None;

        /// <summary>
        /// 대시가 정상 시작된 직후 호출됩니다.
        /// </summary>
        public event Action DashStarted;

        /// <summary>
        /// 대시 종료 애니메이션과 물리 복구까지 모두 끝난 뒤 호출됩니다.
        /// </summary>
        public event Action DashFinished;

        /// <summary>
        /// 대시 후 wait 시간이 만료되어 기존 대시 종료 단계로 넘어가기 직전에 호출됩니다.
        /// </summary>
        public event Action PostDashWaitFinished;

        // --- 캐시 ---
        private Rigidbody2D _rb;
        private Collider2D _col;
        private CharacterPhysicsOverrideController _physicsOverrideController;

        // 애니메이션 길이 캐시
        private Dictionary<string, float> _clipLength = new();

        // --- 파라미터 ---
        private float _dashDistance;               // 월드 유닛
        private float _dashDuration;               // 초
        private Easing.EaseType _easeType = Easing.EaseType.Linear;
        private AnimationCurve _customCurve;       // (선택) 주입 시 커브가 우선

        // --- 내부 계산치 ---
        private Vector2 _dashDir;                  // 좌/우
        private float _elapsed;
        private float _prevS;
        private float _moved;
        private float _waitElapsed;
        private float _postDashWaitSeconds;

        // --- Phase ---
        private enum DashPhase { None, StartOneShot, PlayLoop, WaitLoop, EndOneShot }
        private DashPhase _phase = DashPhase.None;

        // --- 워치독 ---
        private DashPhase _awaitingEventFor = DashPhase.None;
        private float _awaitingDeadline;
        private const float DefaultOneShotTimeout = 0.2f;

        // --- 애니메이션 이름 ---
        private const string AnimDashStart = "dash";
        private const string AnimDashPlay  = "dash_play";
        private const string AnimDashEnd   = "dash_end";

        // --- 충돌 ---
        private LayerMask _blockMask;
        private const float CastSkin = 0.04f;

        // --- 보유 여부 ---
        private bool _hasStart, _hasPlay, _hasWait, _hasEnd;

        // --- 입력/중복 ---
        private bool _isBusy;
        // ActionDash 필드 섹션
        private CharacterPhysicsOverrideHandle _dashGravityOverrideHandle;
        private CharacterAirborneHandle _dashWaitAirborneHandle;
        private float _prevGravityScaleDash;
        private bool  _changedGravityDash; // 대시 중 중력 변경 여부

        #region 초기화/설정

        public override void Initialize(InputManager inputManager, CharacterBase characterBase, CharacterBaseController characterBaseController)
        {
            base.Initialize(inputManager, characterBase, characterBaseController);

            _rb  = actionCharacterBase.characterRigidbody2D;
            _col = actionCharacterBase.colliderMapObject;
            _physicsOverrideController = actionCharacterBase.PhysicsOverrideController;

            if (_rb == null || _col == null)
            {
                GcLogger.LogError("[ActionDash] Rigidbody2D/Collider2D가 필요합니다.");
                return;
            }

            _clipLength = actionCharacterBase.CharacterAnimationController.GetAnimationAllLength();

            int wall   = LayerMask.GetMask(ConfigLayer.GetValue(ConfigLayer.Keys.TileMapWall));
            int ground = LayerMask.GetMask(ConfigLayer.GetValue(ConfigLayer.Keys.TileMapGround));
            _blockMask = wall | ground;
            
            _customCurve  = null; // 기본은 미사용

            _hasStart = HasAnimation(AnimDashStart);
            _hasPlay  = HasAnimation(AnimDashPlay);
            _hasEnd   = HasAnimation(AnimDashEnd);

            actionCharacterBase.OnAnimationEventDash += OnAnimationEventDash;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            ReleaseDashWaitAirborne();
            RestoreGravityAfterDash();
            actionCharacterBase.OnAnimationEventDash -= OnAnimationEventDash;
        }
        
        protected override void ApplySettings()
        {
            _dashDistance = playerActionSettings.dashDistance;
            _dashDuration = playerActionSettings.dashDuration;
            _easeType = playerActionSettings.dashEasing;

            // 진행 중 대시 러너(코루틴/트윈)가 있다면 갱신 로직 추가(선택)
            // e.g. _runner?.UpdateDuration(_duration);
        }

        /// <summary>
        /// 런타임/에디터에서 대시 파라미터 구성
        /// - customCurve가 null이 아니면 커브가 우선 적용됩니다.
        /// </summary>
        public void Configure(float dashDistance, float dashDuration, Easing.EaseType easeType = Easing.EaseType.Linear, AnimationCurve customCurve = null)
        {
            _dashDistance = Mathf.Max(0.01f, dashDistance);
            _dashDuration = Mathf.Max(0.02f, dashDuration);
            _easeType     = easeType;
            _customCurve  = customCurve; // null이면 Easing 사용
        }

        #endregion

        #region 입력/업데이트

        /// <summary>
        /// 현재 상태에서 대시를 시작할 수 있는지 확인합니다.
        /// </summary>
        /// <param name="allowAttackComboWait">
        /// 기존 일반 대시에서는 막는 AttackComboWait 상태를 외부 콤보 규칙에서만 예외적으로 허용할지 여부입니다.
        /// </param>
        /// <returns>대시 시작 조건을 만족하면 <see langword="true"/>입니다.</returns>
        public bool CanBeginDash(bool allowAttackComboWait = false)
        {
            if (_rb == null) return false;
            if (IsHitStopped()) return false;
            if (_isBusy) return false;
            if (actionCharacterBase == null) return false;
            if (actionCharacterBase.IsStatusAttack()) return false;
            if (!allowAttackComboWait && actionCharacterBase.IsStatusAttackComboWait()) return false;
            return true;
        }

        /// <summary>
        /// 대시를 시작합니다.
        /// </summary>
        /// <param name="postDashWaitSeconds">
        /// 이동 대시가 끝난 뒤 <c>dash_wait</c> 애니메이션으로 공중에 머무를 시간입니다.
        /// 0 이하이면 기존처럼 바로 대시 종료 단계로 진입합니다.
        /// </param>
        /// <param name="allowAttackComboWait">
        /// 기존 대시에서는 막는 AttackComboWait 상태를 외부 콤보 규칙에서만 예외적으로 허용할지 여부입니다.
        /// </param>
        /// <returns>대시가 시작되었으면 <see langword="true"/>입니다.</returns>
        public bool Dash(float postDashWaitSeconds = 0f, bool allowAttackComboWait = false)
        {
            if (!CanBeginDash(allowAttackComboWait)) return false;

            // 필요 시 상태 전환: _characterBase.SetStatusDash() 등
            actionCharacterBase.SetStatusDash();
            
            _dashDir  = GetFacingDirection2();
            _isBusy   = true;
            _elapsed  = 0f;
            _prevS    = 0f;
            _moved    = 0f;
            _waitElapsed = 0f;
            _postDashWaitSeconds = Mathf.Max(0f, postDashWaitSeconds);

            EnterPhase(DashPhase.StartOneShot);
            DashStarted?.Invoke();
            return true;
        }

        public void Update()
        {
            if (_rb == null) return;
            if (_phase == DashPhase.None) return;
            if (IsHitStopped()) return;

            // 워치독
            if (_awaitingEventFor != DashPhase.None && Time.unscaledTime >= _awaitingDeadline)
            {
                if (_awaitingEventFor == DashPhase.StartOneShot) HandleDashStartOneShotEnd();
                else if (_awaitingEventFor == DashPhase.EndOneShot) HandleDashEndOneShotEnd();
            }

            if (_phase == DashPhase.PlayLoop)
            {
                float dt = Time.unscaledDeltaTime;
                _elapsed += dt;

                float t = Mathf.Clamp01(_elapsed / _dashDuration);
                float s = Evaluate01(t); // 0~1 진행도

                float deltaDist = Mathf.Max(0f, (s - _prevS) * _dashDistance);

                if (deltaDist > 0f && IsBlockedAhead(deltaDist + CastSkin))
                {
                    EnterPostDashWaitOrEnd();
                    return;
                }

                if (dt > 0f)
                {
                    var v = _rb.GetLinearVelocity();
                    v.x = _dashDir.x * (deltaDist / dt);
                    _rb.SetLinearVelocity(new Vector2(v.x, 0));
                }

                _moved += deltaDist;
                _prevS  = s;

                if (_elapsed >= _dashDuration - 1e-5f || _moved >= _dashDistance - 1e-4f)
                {
                    EnterPostDashWaitOrEnd();
                }
            }

            if (_phase == DashPhase.WaitLoop)
            {
                float dt = Time.unscaledDeltaTime;
                _waitElapsed += dt;
                _rb.SetLinearVelocity(Vector2.zero);

                if (_waitElapsed >= _postDashWaitSeconds)
                {
                    FinishPostDashWait();
                }
            }
        }

        #endregion

        #region 페이즈/이벤트

        private void EnterPhase(DashPhase next)
        {
            _phase = next;
            ClearAwaiting();

            switch (next)
            {
                case DashPhase.StartOneShot:
                    ApplyNoGravityDuringDash();                     // ← 대시 시작 시 중력 제거
                    if (_hasStart)
                    {
                        PlayAnimSafe(AnimDashStart);
                        StartAwaiting(next, AnimDashStart);
                    }
                    else
                    {
                        HandleDashStartOneShotEnd();
                    }
                    break;

                case DashPhase.PlayLoop:
                    ApplyNoGravityDuringDash();                     // ← StartOneShot을 건너뛰는 폴백 대비
                    if (_hasPlay) PlayAnimSafe(AnimDashPlay);
                    break;

                case DashPhase.WaitLoop:
                    ApplyNoGravityDuringDash();
                    AcquireDashWaitAirborne();
                    _rb.SetLinearVelocity(Vector2.zero);
                    _waitElapsed = 0f;
                    if (_hasWait)
                    {
                        PlayAnimSafe(AnimDashPlay, loop: true);
                    }
                    break;

                case DashPhase.EndOneShot:
                    // 여기서는 중력 원복을 하지 않습니다. (엔딩 재생 후 FinishAndStop에서 복구)
                    ReleaseDashWaitAirborne();
                    _rb.SetLinearVelocity(new Vector2(0f, _rb.GetLinearVelocity().y));
                    if (_hasEnd)
                    {
                        PlayAnimSafe(AnimDashEnd);
                        StartAwaiting(next, AnimDashEnd);
                    }
                    else
                    {
                        HandleDashEndOneShotEnd();
                    }
                    break;
            }
        }

        private void HandleDashStartOneShotEnd()
        {
            if (_phase != DashPhase.StartOneShot) return;
            ClearAwaiting();

            if (_hasPlay) PlayAnimSafe(AnimDashPlay);
            _phase = DashPhase.PlayLoop;
        }

        private void HandleDashEndOneShotEnd()
        {
            if (_phase != DashPhase.EndOneShot) return;
            ClearAwaiting();

            _phase  = DashPhase.None;
            _isBusy = false;
            RestoreGravityAfterDash(); // ← 중력 복구
            actionCharacterBase.Stop(); // 필요 시 상태 복귀 커스터마이즈
            _postDashWaitSeconds = 0f;
            _waitElapsed = 0f;
            DashFinished?.Invoke();
        }

        /// <summary>
        /// 이동 대시 이후 설정된 대기 시간이 있으면 wait 단계로, 없으면 기존 종료 단계로 진입합니다.
        /// </summary>
        private void EnterPostDashWaitOrEnd()
        {
            if (_postDashWaitSeconds > 0f)
            {
                EnterPhase(DashPhase.WaitLoop);
                return;
            }

            EnterPhase(DashPhase.EndOneShot);
        }

        /// <summary>
        /// dash_wait 대기 시간이 끝났음을 알리고 기존 대시 종료 단계로 진입합니다.
        /// </summary>
        private void FinishPostDashWait()
        {
            if (_phase != DashPhase.WaitLoop)
            {
                return;
            }

            PostDashWaitFinished?.Invoke();
            if (_phase == DashPhase.WaitLoop)
            {
                EnterPhase(DashPhase.EndOneShot);
            }
        }

        private void StartAwaiting(DashPhase phase, string clipName)
        {
            _awaitingEventFor = phase;
            _awaitingDeadline = Time.unscaledTime + GetClipDurationWithFallback(clipName);
        }

        private void ClearAwaiting()
        {
            _awaitingEventFor = DashPhase.None;
            _awaitingDeadline = 0f;
        }

        private float GetClipDurationWithFallback(string clipName)
        {
            if (_clipLength.TryGetValue(clipName, out var len) && len > 0f) return len + 0.02f;
            return DefaultOneShotTimeout;
        }

        /// <summary>
        /// 대시 애니메이션을 안전하게 재생합니다.
        /// </summary>
        /// <param name="stateName">재생할 애니메이션 상태 이름입니다.</param>
        /// <param name="loop">반복 재생 여부입니다.</param>
        private void PlayAnimSafe(string stateName, bool loop = false)
        {
            actionCharacterBase.CharacterAnimationController?.PlayCharacterAnimation(stateName, loop);
        }

        private void OnAnimationEventDash(CharacterBase sender, EventArgsOnAnimationEventDash e)
        {
            switch (e.EventName)
            {
                case AnimationConstants.EventNameDashPlay:
                    HandleDashStartOneShotEnd();
                    break;
                case AnimationConstants.EventNameDashEnd:
                    HandleDashEndOneShotEnd();
                    break;
            }
        }

        #endregion

        #region 충돌/방향/이징

        private bool IsBlockedAhead(float castDistance)
        {
            if (_col == null || castDistance <= 0f) return false;

            var dir = new Vector2(Mathf.Sign(_dashDir.x), 0f);
            RaycastHit2D[] hits = new RaycastHit2D[2];
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = _blockMask,
                useTriggers = false
            };
            int hitCount = _col.Cast(dir, filter, hits, castDistance);
            return hitCount > 0;
        }

        private Vector2 GetFacingDirection2()
        {
            var facing = actionCharacterBase.CurrentFacing;
            return facing == CharacterConstants.FacingDirection8.Left ? Vector2.left : Vector2.right;
        }

        /// <summary>
        /// 0~1 → 0~1 진행도 평가
        /// - 커스텀 커브가 설정되어 있으면 그 값을 우선 사용
        /// - 없으면 GGemCo2DCore.Easing.Apply 사용
        /// </summary>
        private float Evaluate01(float t01)
        {
            t01 = Mathf.Clamp01(t01);
            if (_customCurve != null) return Mathf.Clamp01(_customCurve.Evaluate(t01));
            return Mathf.Clamp01(Easing.Apply(t01, _easeType));
        }

        #endregion

        /// <summary>
        /// 대시를 강제로 중단합니다.
        /// skipEndAnimation == false : 가능한 경우 'dash_end'를 재생하며 정상 종료 단계로 전환.
        /// skipEndAnimation == true  : 애니메이션을 스킵하고 즉시 정지.
        /// 어느 Phase에서 호출돼도 안전합니다.
        /// </summary>
        public void CancelDash(bool skipEndAnimation = false)
        {
            if (_rb == null) return;
            if (_phase == DashPhase.None) return;

            ClearAwaiting();
            ReleaseDashWaitAirborne();

            if (skipEndAnimation || !_hasEnd)
            {
                _rb.SetLinearVelocity(new Vector2(0f, _rb.GetLinearVelocity().y));
                _phase  = DashPhase.None;
                _isBusy = false;
                _postDashWaitSeconds = 0f;
                _waitElapsed = 0f;

                RestoreGravityAfterDash();        // ← 즉시 취소 시 바로 복구
                actionCharacterBase.Stop();
                DashFinished?.Invoke();
                return;
            }

            // 엔딩 애니메이션 재생하며 종료 → FinishAndStop()에서 복구
            EnterPhase(DashPhase.EndOneShot);
        }

        // ActionDash 내부
        private void ApplyNoGravityDuringDash()
        {
            if (_rb == null || _changedGravityDash) return;

            if (_physicsOverrideController != null)
            {
                _dashGravityOverrideHandle = _physicsOverrideController.AcquireGravityOverride(
                    ownerKey: this,
                    lifecycleOwner: actionCharacterBase,
                    channel: CharacterPhysicsOverrideChannel.Action,
                    priority: CharacterPhysicsOverridePriority.ActionDash,
                    gravityScale: 0f,
                    reason: "ActionDash");

                _changedGravityDash = _dashGravityOverrideHandle.IsValid;
                return;
            }

            _prevGravityScaleDash = _rb.gravityScale;
            _rb.gravityScale = 0f;                // 대시 동안 중력 제거
            _changedGravityDash = true;
        }

        private void RestoreGravityAfterDash()
        {
            if (_rb == null) return;
            if (!_changedGravityDash) return;

            if (_dashGravityOverrideHandle.IsValid && _physicsOverrideController != null)
            {
                _physicsOverrideController.ReleaseGravityOverride(ref _dashGravityOverrideHandle);
            }
            else
            {
                _rb.gravityScale = _prevGravityScaleDash; // 원복
            }

            _dashGravityOverrideHandle = default;
            _changedGravityDash = false;
        }

        /// <summary>
        /// 대시 후 wait 단계 동안 다른 시스템이 캐릭터를 지상 상태로 오인하지 않도록 강제 공중 토큰을 등록합니다.
        /// </summary>
        private void AcquireDashWaitAirborne()
        {
            if (_dashWaitAirborneHandle.IsValid || actionCharacterBase == null)
            {
                return;
            }

            _dashWaitAirborneHandle = actionCharacterBase.AcquireAirborne(
                CharacterAirborneSource.External,
                "ActionDash.Wait");
        }

        /// <summary>
        /// 대시 wait 단계에서 등록한 강제 공중 토큰을 해제합니다.
        /// </summary>
        private void ReleaseDashWaitAirborne()
        {
            if (!_dashWaitAirborneHandle.IsValid || actionCharacterBase == null)
            {
                _dashWaitAirborneHandle = default;
                return;
            }

            actionCharacterBase.ReleaseAirborne(_dashWaitAirborneHandle);
            _dashWaitAirborneHandle = default;
        }

    }
}
