using System;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 모바일 온스크린 HUD 설정입니다.
    /// 프리팹이 없으면 런타임 생성형 기본 HUD를 사용합니다.
    /// </summary>
    [CreateAssetMenu(fileName = ConfigScriptableObjectControl.MobileHud.FileName, menuName = ConfigScriptableObjectControl.MobileHud.MenuName, order = ConfigScriptableObjectControl.MobileHud.Ordering)]
    public class GGemCoMobileHudSettings : ScriptableObject, ISettingsChangeNotifier
    {
        public enum LayoutPreset
        {
            Default,
            Compact,
            Wide,
        }

        public event Action Changed;

#if UNITY_EDITOR
        private void OnValidate()
        {
            globalAlpha = Mathf.Clamp(globalAlpha, 0.05f, 1f);
            buttonAlpha = Mathf.Clamp(buttonAlpha, 0.05f, 1f);
            joystickKnobAlpha = Mathf.Clamp(joystickKnobAlpha, 0.05f, 1f);
            joystickMovementRange = Mathf.Max(1f, joystickMovementRange);
            joystickDeadZone = Mathf.Clamp01(joystickDeadZone);
            tabletScale = Mathf.Max(0.5f, tabletScale);
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

        [Header("프리팹/아이콘")]
        public GameObject hudPrefab;
        public Sprite attackIcon;
        public Sprite jumpIcon;
        public Sprite dashIcon;
        public Sprite guardIcon;
        public Sprite interactionIcon;
        public Sprite joystickBaseSprite;
        public Sprite joystickKnobSprite;
        public Sprite buttonBackgroundSprite;

        [Header("표시")]
        [Range(0.05f, 1f)] public float globalAlpha = 1f;
        [Range(0.05f, 1f)] public float buttonAlpha = 0.68f;
        [Range(0.05f, 1f)] public float joystickKnobAlpha = 0.88f;
        public int canvasSortingOrder = 500;

        [Header("레이아웃")]
        public LayoutPreset layoutPreset = LayoutPreset.Default;
        public bool swapLeftRightHand;
        public float bottomPadding = 28f;
        public float sidePadding = 24f;
        public Vector2 leftPanelOffset = Vector2.zero;
        public Vector2 rightPanelOffset = Vector2.zero;
        public Vector2 safeAreaPadding = Vector2.zero;
        public float tabletScale = 1f;

        [Header("조이스틱")]
        public float joystickAreaSize = 300f;
        public float joystickBaseSize = 176f;
        public float joystickKnobSize = 82f;
        public float joystickMovementRange = 68f;
        [Range(0f, 1f)] public float joystickDeadZone = 0.1f;

        [Header("버튼")]
        public float actionAreaSize = 320f;
        public Vector2 buttonSize = new Vector2(88f, 88f);
        public float buttonSpacing = 12f;
        [Range(0.05f, 1f)] public float holdButtonPressedAlpha = 0.92f;
    }
}
