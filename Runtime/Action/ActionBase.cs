using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    public abstract class ActionBase
    {
        // --- 외부 참조 ---
        protected InputManager actionInputManager;
        public CharacterBase actionCharacterBase;
        protected CharacterBaseController actionCharacterBaseController;
        protected GGemCoPlayerActionSettings playerActionSettings;
        protected GGemCoPlayerGuardSettings playerGuardSettings;

        public virtual void Initialize(InputManager inputManager, CharacterBase characterBase,
            CharacterBaseController characterBaseController)
        {
            actionInputManager = inputManager;
            actionCharacterBase = characterBase;
            actionCharacterBaseController = characterBaseController;
            playerActionSettings = AddressableLoaderSettingsControl.Instance.playerActionSettings;
            playerGuardSettings = AddressableLoaderSettingsControl.Instance.playerGuardSettings;
#if UNITY_EDITOR
            // 플레이 중 인스펙터 수정 → 즉시 반영
            if (playerActionSettings != null)
            {
                playerActionSettings.Changed += ApplySettings;
            }

            if (playerGuardSettings != null)
            {
                playerGuardSettings.Changed += ApplySettings;
            }
#endif
            ApplySettings();
        }
        public virtual void OnDestroy()
        {
#if UNITY_EDITOR
            if (playerActionSettings != null)
            {
                playerActionSettings.Changed -= ApplySettings;
            }

            if (playerGuardSettings != null)
            {
                playerGuardSettings.Changed -= ApplySettings;
            }
#endif
        }

        protected abstract void ApplySettings();

        protected bool IsHitStopped()
        {
            return actionCharacterBase != null && actionCharacterBase.IsHitStopped;
        }

        protected float AdvanceHitStopAwareUnscaled(ref float elapsedSeconds)
        {
            if (IsHitStopped())
            {
                return 0f;
            }

            float delta = Time.unscaledDeltaTime;
            elapsedSeconds += delta;
            return delta;
        }

        protected bool HasAnimation(string stateName)
        {
            // 1) 캐릭터 애니메이션 컨트롤러가 "존재 여부"를 제공한다면 우선 사용
            if (actionCharacterBase.CharacterAnimationController is { } ctrl)
            {
                // 선택: ctrl에 HasAnimation(string) API가 있다면 사용하도록 교체 가능
                return ctrl.HasAnimation(stateName);
            }

            return false;

            // // 2) Animator의 클립 이름으로 보수적 판단
            // return _clipLength.ContainsKey(stateName);
        }
    }
}