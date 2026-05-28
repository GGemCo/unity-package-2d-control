using System;
using GGemCo2DCore;
using R3;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 플레이어 탈진 상태를 관리합니다.
    /// - 스테미나가 0이 되는 순간 즉시 탈진에 진입합니다.
    /// - 탈진 중에는 제어 잠금 + DontMove 상태를 유지합니다.
    /// - 지정 시간 동안 최대 스태미나 비율만큼 전용 회복을 수행합니다.
    /// - 피격으로 Damage 애니메이션에 들어가도 탈진 회복은 계속되며, 종료 후 루프 애니메이션으로 복귀합니다.
    /// </summary>
    internal sealed class PlayerExhaustionController : IDisposable
    {
        private enum ExhaustionPhase
        {
            None,
            Start,
            RecoverLoop,
            End,
        }

        private readonly CharacterBase _character;
        private readonly ActionGuard _guard;
        private readonly Action _onEnterExhaustion;

        private IDisposable _staminaSubscription;
        private object _controlLockToken;

        private bool _enabled;
        private bool _resumeLoopAfterHit = true;

        private float _durationSeconds;
        private float _recoverRatio;

        private string _startAnimationName = "Moveset_exhaustionStart";
        private string _loopAnimationName = "Moveset_exhaustionLoop";
        private string _endAnimationName = "Moveset_exhaustionEnd";

        private bool _hasStartAnimation;
        private bool _hasLoopAnimation;
        private bool _hasEndAnimation;

        private ExhaustionPhase _phase;
        private float _phaseElapsed;
        private float _recoverElapsed;
        private float _recoverAccumulator;
        private float _recoverPerSecond;
        private float _startDurationSeconds;
        private float _endDurationSeconds;
        private long _recoverTargetStamina;
        private bool _loopReplayPending;
        private bool _pendingEndAfterDamage;
        private bool _lastObservedDepleted;

        public bool IsExhausting => _phase != ExhaustionPhase.None;

        public event Action<bool> StateChanged;

        public PlayerExhaustionController(CharacterBase character, ActionGuard guard, Action onEnterExhaustion)
        {
            _character = character;
            _guard = guard;
            _onEnterExhaustion = onEnterExhaustion;

            if (_character == null)
                return;

            _lastObservedDepleted = IsStaminaDepleted(_character.CurrentStamina.Value);
            _staminaSubscription = _character.CurrentStamina.Subscribe(OnCurrentStaminaChanged);
        }

        /// <summary>
        /// 가드 설정 자산의 탈진 정책을 런타임 캐시에 반영합니다.
        /// </summary>
        /// <param name="settings">플레이어 가드 설정 자산입니다.</param>
        public void ApplySettings(GGemCoPlayerGuardSettings settings)
        {
            if (settings == null)
            {
                _enabled = false;
                ResetState(releaseControlLock: true, stopToIdle: _character != null && !_character.IsStatusDamage() && !_character.IsStatusDead());
                return;
            }

            _enabled = settings.enableExhaustion;
            _durationSeconds = Mathf.Max(0f, settings.exhaustionDurationSeconds);
            _recoverRatio = Mathf.Clamp01(settings.exhaustionRecoverMaxRatio);
            _resumeLoopAfterHit = settings.resumeExhaustionLoopAfterHit;

            _startAnimationName = string.IsNullOrWhiteSpace(settings.exhaustionStartAnimation)
                ? "Moveset_exhaustionStart"
                : settings.exhaustionStartAnimation;
            _loopAnimationName = string.IsNullOrWhiteSpace(settings.exhaustionLoopAnimation)
                ? "Moveset_exhaustionLoop"
                : settings.exhaustionLoopAnimation;
            _endAnimationName = string.IsNullOrWhiteSpace(settings.exhaustionEndAnimation)
                ? "Moveset_exhaustionEnd"
                : settings.exhaustionEndAnimation;

            CacheAnimationAvailability();

            if (!_enabled)
            {
                ResetState(releaseControlLock: true, stopToIdle: _character != null && !_character.IsStatusDamage() && !_character.IsStatusDead());
                return;
            }

            // 설정이 바뀐 뒤 현재 값이 이미 0이면 다음 0 진입까지 기다리도록 스냅샷만 갱신합니다.
            _lastObservedDepleted = IsStaminaDepleted(_character != null ? _character.CurrentStamina.Value : 0L);
        }

        public void Tick(float deltaTime)
        {
            if (_character == null)
                return;

            if (_character.IsStatusDead())
            {
                ResetState(releaseControlLock: true, stopToIdle: false);
                return;
            }

            if (!IsExhausting)
                return;

            if (_pendingEndAfterDamage)
            {
                if (_character.IsStatusDamage())
                    return;

                BeginEndPhase();
            }

            switch (_phase)
            {
                case ExhaustionPhase.Start:
                    TickRecovery(deltaTime);
                    _phaseElapsed += Mathf.Max(0f, deltaTime);

                    if (ShouldFinishRecovery())
                    {
                        RequestEndOrDeferUntilDamageEnds();
                        return;
                    }

                    if (_startDurationSeconds <= 0f || _phaseElapsed >= _startDurationSeconds)
                    {
                        BeginRecoverLoopPhase();
                    }
                    break;

                case ExhaustionPhase.RecoverLoop:
                    TickRecovery(deltaTime);
                    EnsureRecoverLoopAnimation();

                    if (ShouldFinishRecovery())
                    {
                        RequestEndOrDeferUntilDamageEnds();
                    }
                    break;

                case ExhaustionPhase.End:
                    _phaseElapsed += Mathf.Max(0f, deltaTime);
                    if (_endDurationSeconds <= 0f || _phaseElapsed >= _endDurationSeconds)
                    {
                        FinishExhaustion();
                    }
                    break;
            }
        }

        public void Dispose()
        {
            _staminaSubscription?.Dispose();
            _staminaSubscription = null;
            ResetState(releaseControlLock: true, stopToIdle: false);
        }

        private void OnCurrentStaminaChanged(long currentStamina)
        {
            bool isDepleted = IsStaminaDepleted(currentStamina);

            if (_character == null)
            {
                _lastObservedDepleted = isDepleted;
                return;
            }

            if (!_enabled || _character.IsStatusDead())
            {
                _lastObservedDepleted = isDepleted;
                return;
            }

            if (!_lastObservedDepleted && isDepleted && !IsExhausting)
            {
                EnterExhaustion();
            }

            _lastObservedDepleted = isDepleted;
        }

        private bool IsStaminaDepleted(long currentStamina)
        {
            if (_character == null)
                return false;

            return _character.TotalStamina.Value > 0 && currentStamina <= 0;
        }

        private void CacheAnimationAvailability()
        {
            var animationController = _character != null ? _character.CharacterAnimationController : null;
            if (animationController == null)
            {
                _hasStartAnimation = false;
                _hasLoopAnimation = false;
                _hasEndAnimation = false;
                _startDurationSeconds = 0f;
                _endDurationSeconds = 0f;
                return;
            }

            _hasStartAnimation = !string.IsNullOrWhiteSpace(_startAnimationName) && animationController.HasAnimation(_startAnimationName);
            _hasLoopAnimation = !string.IsNullOrWhiteSpace(_loopAnimationName) && animationController.HasAnimation(_loopAnimationName);
            _hasEndAnimation = !string.IsNullOrWhiteSpace(_endAnimationName) && animationController.HasAnimation(_endAnimationName);
            _startDurationSeconds = _hasStartAnimation ? animationController.GetCharacterAnimationDuration(_startAnimationName, false) : 0f;
            _endDurationSeconds = _hasEndAnimation ? animationController.GetCharacterAnimationDuration(_endAnimationName, false) : 0f;
        }

        private void EnterExhaustion()
        {
            if (_character == null || !_enabled)
                return;

            bool wasExhausting = IsExhausting;

            _onEnterExhaustion?.Invoke();

            if (_controlLockToken == null)
            {
                _controlLockToken = _character.AcquireControlLock(this);
            }

            _character.directionNormalize = Vector3.zero;
            _character.SetStatusDontMove();

            _phase = ExhaustionPhase.Start;
            _phaseElapsed = 0f;
            _recoverElapsed = 0f;
            _recoverAccumulator = 0f;
            _loopReplayPending = false;
            _pendingEndAfterDamage = false;

            long maxStamina = Math.Max(0L, _character.TotalStamina.Value);
            _recoverTargetStamina = Math.Max(0L, Mathf.CeilToInt(maxStamina * _recoverRatio));
            if (_recoverTargetStamina > maxStamina)
            {
                _recoverTargetStamina = maxStamina;
            }
            if (_recoverRatio > 0f && maxStamina > 0 && _recoverTargetStamina <= 0)
            {
                _recoverTargetStamina = 1;
            }

            _recoverPerSecond = (_durationSeconds > 0f && _recoverTargetStamina > 0)
                ? _recoverTargetStamina / _durationSeconds
                : 0f;

            if (_guard != null && _guard.IsGuarding)
            {
                _guard.CancelGuard(true, false);
                _character.SetStatusDontMove();
            }

            if (_hasStartAnimation)
            {
                _character.CharacterAnimationController?.PlayCharacterAnimation(_startAnimationName);
            }
            else
            {
                BeginRecoverLoopPhase();
            }

            if (!wasExhausting)
            {
                StateChanged?.Invoke(true);
            }
        }

        private void TickRecovery(float deltaTime)
        {
            if (_character == null)
                return;

            if (_recoverPerSecond <= 0f)
                return;

            float safeDelta = Mathf.Max(0f, deltaTime);
            _recoverElapsed += safeDelta;
            _recoverAccumulator += _recoverPerSecond * safeDelta;

            if (_recoverAccumulator < 1f)
                return;

            long amount = Mathf.FloorToInt(_recoverAccumulator);
            _recoverAccumulator -= amount;

            if (amount <= 0)
                return;

            long before = _character.CurrentStamina.Value;
            if (before >= _recoverTargetStamina)
                return;

            long needed = _recoverTargetStamina - before;
            if (amount > needed)
                amount = needed;

            if (amount > 0)
            {
                _character.RestoreStamina(amount);
            }
        }

        private bool ShouldFinishRecovery()
        {
            if (_character == null)
                return true;

            if (_recoverTargetStamina > 0 && _character.CurrentStamina.Value >= _recoverTargetStamina)
                return true;

            if (_durationSeconds > 0f && _recoverElapsed >= _durationSeconds)
                return true;

            return _durationSeconds <= 0f;
        }

        private void BeginRecoverLoopPhase()
        {
            _phase = ExhaustionPhase.RecoverLoop;
            _phaseElapsed = 0f;
            _loopReplayPending = true;
            EnsureRecoverLoopAnimation();
        }

        private void EnsureRecoverLoopAnimation()
        {
            if (_phase != ExhaustionPhase.RecoverLoop || _character == null)
                return;

            if (_character.IsStatusDamage())
            {
                if (_resumeLoopAfterHit)
                {
                    _loopReplayPending = true;
                }
                return;
            }

            _character.SetStatusDontMove();

            if (!_hasLoopAnimation)
            {
                _loopReplayPending = false;
                return;
            }

            if (!_loopReplayPending)
                return;

            _character.CharacterAnimationController?.PlayCharacterAnimation(_loopAnimationName, true);
            _loopReplayPending = false;
        }

        private void RequestEndOrDeferUntilDamageEnds()
        {
            if (_character == null)
                return;

            if (_character.IsStatusDamage())
            {
                _pendingEndAfterDamage = true;
                return;
            }

            BeginEndPhase();
        }

        private void BeginEndPhase()
        {
            if (_character == null)
                return;

            _pendingEndAfterDamage = false;
            _phase = ExhaustionPhase.End;
            _phaseElapsed = 0f;
            _character.SetStatusDontMove();

            if (_hasEndAnimation)
            {
                _character.CharacterAnimationController?.PlayCharacterAnimation(_endAnimationName);
            }
            else
            {
                FinishExhaustion();
            }
        }

        private void FinishExhaustion()
        {
            if (_character == null)
                return;

            ResetState(releaseControlLock: true, stopToIdle: !_character.IsStatusDamage() && !_character.IsStatusDead());
        }

        private void ResetState(bool releaseControlLock, bool stopToIdle)
        {
            bool wasExhausting = IsExhausting;

            _phase = ExhaustionPhase.None;
            _phaseElapsed = 0f;
            _recoverElapsed = 0f;
            _recoverAccumulator = 0f;
            _recoverPerSecond = 0f;
            _recoverTargetStamina = 0;
            _loopReplayPending = false;
            _pendingEndAfterDamage = false;

            if (releaseControlLock && _character != null && _controlLockToken != null)
            {
                _character.ReleaseControlLock(_controlLockToken);
                _controlLockToken = null;
            }

            if (stopToIdle && _character != null)
            {
                _character.Stop(true);
            }
            if (wasExhausting)
            {
                StateChanged?.Invoke(false);
            }
        }
    }
}
