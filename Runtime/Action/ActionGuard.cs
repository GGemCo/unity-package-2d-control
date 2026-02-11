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

        // --- 애니메이션 존재 여부 캐시 ---
        private bool _hasStart, _hasWait, _hasEnd;
        
        // [Tooltip("방어 시작시 차감되는 스테미나")]
        private long _guardStartStaminaCost;

        // [Tooltip("방어 성공시 차감되는 스테미나")]
        private long _guardSuccessStaminaCost;

        // [Tooltip("가드를 하는 중이면, 몇 초 마다 차감할 것인지")]
        private float _guardStaminaTickInterval;

        // [Tooltip("guardStaminaTickInterval 시간마다 얼마나 차감할 것인지")]
        private long _guardStaminaTickCost;

        // 스테미나 틱 누적(프레임 드랍 보정)
        private float _staminaTickElapsed;

        public override void Initialize(InputManager inputManager, CharacterBase characterBase, CharacterBaseController characterBaseController)
        {
            // ApplySettings에서 playerActionSettings 사용
            base.Initialize(inputManager, characterBase, characterBaseController);
            actionCharacterBase.OnAnimationEventGuardEnd += OnAnimationEventGuardEnd;
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
            _hasStart = HasAnimation(_animGuardStart);
            _hasWait = HasAnimation(_animGuardWait);
            _hasEnd = HasAnimation(_animGuardEnd);
        }

        private void InitializeGuardSettings()
        {
            if (!playerActionSettings) return;
            _guardStartStaminaCost = playerActionSettings.guardStartStaminaCost;
            _guardSuccessStaminaCost = playerActionSettings.guardSuccessStaminaCost;
            _guardStaminaTickInterval = playerActionSettings.guardStaminaTickInterval;
            _guardStaminaTickCost = playerActionSettings.guardStaminaTickCost;
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
                CancelGuard(true);
                return;
            }

            // 스테미나 0이면 입력과 무관하게 즉시 해제
            if (actionCharacterBase.CurrentStamina.Value <= 0)
            {
                CancelGuard(true);
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
                    CancelGuard(true);
                    return;
                }

                if (actionCharacterBase.CurrentStamina.Value <= 0)
                {
                    CancelGuard(true);
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
                CancelGuard(true);
                return false;
            }

            if (actionCharacterBase.CurrentStamina.Value <= 0)
            {
                CancelGuard(true);
                return false;
            }

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

        private void BeginEnd()
        {
            if (_phase == GuardPhase.End) return;

            _phase = GuardPhase.End;

            if (_hasEnd)
            {
                actionCharacterBase.CharacterAnimationController?.PlayCharacterAnimation(_animGuardEnd);
            }
            else
            {
                FinishGuard();
            }
        }

        private void OnAnimationEventGuardEnd(CharacterBase sender, EventArgsOnAnimationEventGuardEnd e)
        {
            FinishGuard();
        }

        private void FinishGuard()
        {
            _phase = GuardPhase.None;
            _staminaTickElapsed = 0f;
            // 상태 복귀는 Stop이 담당(기존 설계 유지)
            actionCharacterBase?.Stop(true);
        }

        public void CancelGuard(bool skipEndAnimation = false)
        {
            if (skipEndAnimation || !_hasEnd)
            {
                FinishGuard();
                return;
            }

            BeginEnd();
        }

        private bool TrySpendStamina(long amount)
        {
            if (actionCharacterBase == null) return false;

            if (amount <= 0) return true;

            return actionCharacterBase.TrySpendStamina(amount);
        }
    }
}
