using System;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 모바일 온스크린 HUD 설정입니다.
    /// 샘플 HUD 프리팹과 입력 동작 관련 최소 설정만 보관합니다.
    /// </summary>
    [CreateAssetMenu(fileName = ConfigScriptableObjectControl.MobileHud.FileName, menuName = ConfigScriptableObjectControl.MobileHud.MenuName, order = ConfigScriptableObjectControl.MobileHud.Ordering)]
    public class GGemCoMobileHudSettings : ScriptableObject, ISettingsChangeNotifier
    {
        public event Action Changed;

#if UNITY_EDITOR
        private void OnValidate()
        {
            joystickMovementRange = Mathf.Max(1f, joystickMovementRange);
            joystickDeadZone = Mathf.Clamp01(joystickDeadZone);
            Changed?.Invoke();
        }
#endif

        public void RaiseChanged()
        {
            Changed?.Invoke();
        }

        [Header("활성화")]
        public bool enableMobileHud = true;
        public bool enableOnMobilePlatformOnly = true;
        public bool forceShowInEditor;
        public bool useSafeArea = true;
        public bool autoHideWhenNonTouchScheme;

        [Header("프리팹")]
        public GameObject hudPrefab;

        [Header("조이스틱")]
        public float joystickMovementRange = 68f;
        [Range(0f, 1f)] public float joystickDeadZone = 0.1f;
    }
}
