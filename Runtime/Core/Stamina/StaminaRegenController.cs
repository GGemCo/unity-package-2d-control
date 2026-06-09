using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 스테미나 회복 정책 컨트롤러
    /// - Control 패키지에서 "언제 회복하는가" 정책만 담당합니다.
    /// - 실제 스테미나 값 변경은 Core(CharacterBase)의 RestoreStamina API를 통해서만 수행합니다.
    /// </summary>
    internal sealed class StaminaRegenController
    {
        private readonly CharacterBase _character;
        private readonly ActionGuard _guard;

        private float _interval;
        private ConfigCommon.CalculateType _valueType;
        private float _value;

        private float _acc;

        public StaminaRegenController(CharacterBase character, ActionGuard guard)
        {
            _character = character;
            _guard = guard;
        }

        /// <summary>
        /// 가드 설정 자산의 스테미나 회복 정책을 런타임 캐시에 반영합니다.
        /// </summary>
        /// <param name="settings">플레이어 가드 설정 자산입니다.</param>
        public void ApplySettings(GGemCoPlayerGuardSettings settings)
        {
            if (!settings)
            {
                _interval = 0f;
                _valueType = ConfigCommon.CalculateType.Flat;
                _value = 0f;
                _acc = 0f;
                return;
            }

            _interval = Mathf.Max(0f, settings.noGuardStaminaTickInterval);
            _valueType = settings.noGuardStaminaTickValueType;
            _value = Mathf.Max(0f, settings.noGuardStaminaTickValue);
        }

        /// <summary>
        /// Update 루프에서 호출되는 틱.
        /// - 프레임 드랍에도 회복 틱이 누락되지 않도록 누적 시간 + while 처리.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_character == null) return;
            if (_character.IsStatusDead())
            {
                _acc = 0f;
                return;
            }

            // 설정이 비활성화된 경우
            if (_interval <= 0f || _value <= 0f)
            {
                _acc = 0f;
                return;
            }

            // 가드 중이면 회복하지 않는다(입력 상태와 무관)
            if (_guard is { IsGuarding: true })
            {
                _acc = 0f;
                return;
            }

            // 이미 가득 차 있으면 누적하지 않는다(해제 직후 즉시 회복 방지)
            if (_character.CurrentStamina.Value >= _character.MaxStamina.Value)
            {
                _acc = 0f;
                return;
            }

            _acc += deltaTime;
            if (_acc < _interval) return;

            // 매우 큰 deltaTime에도 안정적으로 처리
            while (_acc >= _interval)
            {
                _acc -= _interval;
                long amount = ComputeRegenAmount();
                if (amount > 0)
                {
                    _character.RestoreStamina(amount);
                }

                if (_character.CurrentStamina.Value >= _character.MaxStamina.Value)
                {
                    _acc = 0f;
                    break;
                }
            }
        }

        private long ComputeRegenAmount()
        {
            if (_character == null) return 0;

            switch (_valueType)
            {
                case ConfigCommon.CalculateType.Flat:
                    return Mathf.Max(0, Mathf.RoundToInt(_value));

                case ConfigCommon.CalculateType.PercentOfMax:
                {
                    long max = _character.MaxStamina.Value;

                    // _value: 0~1 (예: 0.02f = 2%)
                    float ratio = Mathf.Clamp01(_value);
                    long amount = Mathf.RoundToInt(max * ratio);

                    // 너무 작은 비율로 0이 되는 경우를 방지(정책: 최소 1)
                    return amount <= 0 ? 1 : amount;
                }
                default:
                    return 0;
            }
        }
    }
}