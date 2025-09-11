using GGemCo2DCore;

namespace GGemCo2DControl
{
    public abstract class ActionBase
    {
        // --- 외부 참조 ---
        protected InputManager actionInputManager;
        protected CharacterBase actionCharacterBase;
        protected CharacterBaseController actionCharacterBaseController;
        protected GGemCoPlayerActionSettings playerActionSettings;

        public virtual void Initialize(InputManager inputManager, CharacterBase characterBase,
            CharacterBaseController characterBaseController)
        {
            actionInputManager = inputManager;
            actionCharacterBase = characterBase;
            actionCharacterBaseController = characterBaseController;
            playerActionSettings = AddressableLoaderSettingsControl.Instance.playerActionSettings;
#if UNITY_EDITOR
            // 플레이 중 인스펙터 수정 → 즉시 반영
            playerActionSettings.Changed += ApplySettings;
#endif
            ApplySettings();
        }
        public virtual void OnDestroy()
        {
#if UNITY_EDITOR
            playerActionSettings.Changed -= ApplySettings;
#endif
        }

        protected abstract void ApplySettings();
    }
}