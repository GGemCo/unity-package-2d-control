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

        // 스테미나 틱(유지 차감) 누적 시간
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

            // Guard 시작 비용(즉시 1회 차감)
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
        /// Guard 유지 중(Start/Wait) 스테미나 틱 차감 처리.
        /// - 프레임 드랍 시에도 누락 없이 처리하기 위해 누적 시간 + while 루프를 사용합니다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_phase != GuardPhase.Start && _phase != GuardPhase.Wait) return;
            if (_guardStaminaTickInterval <= 0f) return;

            // 비정상 값 방어
            if (deltaTime <= 0f) return;

            _staminaTickElapsed += deltaTime;

            // 1프레임에 여러 번 차감될 수 있음(프레임 드랍)
            while (_staminaTickElapsed >= _guardStaminaTickInterval)
            {
                _staminaTickElapsed -= _guardStaminaTickInterval;

                if (!TrySpendStamina(_guardStaminaTickCost))
                {
                    // 스테미나 부족 시 즉시 해제(연출 스킵)
                    CancelGuard(skipEndAnimation: false);
                    return;
                }
            }
        }

        /// <summary>
        /// 가드 성공(블록/저스트가드 등) 확정 시 호출.
        /// - 성공 비용을 지불할 수 없으면 즉시 가드를 해제하고 false를 반환합니다.
        /// </summary>
        public bool OnGuardSuccess()
        {
            if (!IsGuarding) return false;

            if (!TrySpendStamina(_guardSuccessStaminaCost))
            {
                CancelGuard(skipEndAnimation: true);
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
