using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GGemCo2DControl
{
    /// <summary>
    /// 뷰, 설정, InputAction 바인딩을 연결합니다.
    /// 프리팹이 없으면 런타임 생성형 기본 뷰를 구성합니다.
    /// </summary>
    public sealed class MobileInputHudPresenter
    {
        private static Sprite s_DefaultCircleSprite;

        public void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                if (EventSystem.current.GetComponent<InputSystemUIInputModule>() == null)
                {
                    EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                return;
            }

            GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(eventSystemGo);
        }

        public MobileHudRootView CreateOrInstantiateView(GGemCoMobileHudSettings settings)
        {
            if (settings != null && settings.hudPrefab != null)
            {
                GameObject instance = Object.Instantiate(settings.hudPrefab);
                instance.name = settings.hudPrefab.name;
                Object.DontDestroyOnLoad(instance);

                MobileHudRootView prefabView = instance.GetComponent<MobileHudRootView>();
                if (prefabView == null)
                {
                    prefabView = instance.GetComponentInChildren<MobileHudRootView>(true);
                }

                if (prefabView != null)
                {
                    prefabView.EnsureReferences();
                    return prefabView;
                }

                Object.Destroy(instance);
            }

            return CreateRuntimeView(settings);
        }

        public void Apply(MobileHudRootView rootView, GGemCoMobileHudSettings settings, PlayerInput playerInput, MobileInputBindingResolver bindingResolver)
        {
            if (rootView == null || settings == null)
            {
                return;
            }

            rootView.EnsureReferences();

            if (rootView.Canvas != null)
            {
                rootView.Canvas.sortingOrder = settings.canvasSortingOrder;
            }

            if (rootView.RootCanvasGroup != null)
            {
                rootView.RootCanvasGroup.alpha = settings.globalAlpha;
                rootView.RootCanvasGroup.interactable = true;
                rootView.RootCanvasGroup.blocksRaycasts = true;
            }

            rootView.SafeAreaFitter?.Initialize(rootView.SafeAreaRoot, settings.useSafeArea, settings.safeAreaPadding);

            ApplyLayout(rootView, settings);
            ApplyJoystick(rootView.JoystickView, settings, playerInput, bindingResolver);
            ApplyButtons(rootView, settings, playerInput, bindingResolver);
        }

        private static void ApplyLayout(MobileHudRootView rootView, GGemCoMobileHudSettings settings)
        {
            if (rootView.LeftPanel != null)
            {
                rootView.LeftPanel.sizeDelta = new Vector2(settings.joystickAreaSize, settings.joystickAreaSize);
                rootView.LeftPanel.anchoredPosition = new Vector2(settings.sidePadding, settings.bottomPadding) + settings.leftPanelOffset;
            }

            if (rootView.RightPanel != null)
            {
                rootView.RightPanel.sizeDelta = new Vector2(settings.actionAreaSize, settings.actionAreaSize);
                rootView.RightPanel.anchoredPosition = new Vector2(-settings.sidePadding, settings.bottomPadding) + settings.rightPanelOffset;
            }

            if (!settings.swapLeftRightHand)
            {
                return;
            }

            if (rootView.LeftPanel != null)
            {
                rootView.LeftPanel.anchorMin = new Vector2(1f, 0f);
                rootView.LeftPanel.anchorMax = new Vector2(1f, 0f);
                rootView.LeftPanel.anchoredPosition = new Vector2(-settings.sidePadding, settings.bottomPadding) + settings.leftPanelOffset;
            }

            if (rootView.RightPanel != null)
            {
                rootView.RightPanel.anchorMin = new Vector2(0f, 0f);
                rootView.RightPanel.anchorMax = new Vector2(0f, 0f);
                rootView.RightPanel.anchoredPosition = new Vector2(settings.sidePadding, settings.bottomPadding) + settings.rightPanelOffset;
            }
        }

        private static void ApplyJoystick(MobileHudJoystickView joystickView, GGemCoMobileHudSettings settings, PlayerInput playerInput, MobileInputBindingResolver bindingResolver)
        {
            if (joystickView == null)
            {
                return;
            }

            joystickView.ApplyVisual(
                baseAlpha: settings.buttonAlpha,
                knobAlpha: settings.joystickKnobAlpha,
                baseSprite: settings.joystickBaseSprite != null ? settings.joystickBaseSprite : GetCircleSprite(),
                knobSprite: settings.joystickKnobSprite != null ? settings.joystickKnobSprite : GetCircleSprite(),
                baseSize: settings.joystickBaseSize,
                knobSize: settings.joystickKnobSize,
                movementRange: settings.joystickMovementRange);

            OnScreenStick stick = joystickView.OnScreenStick;
            if (stick == null) return;
            
            var targetPath = bindingResolver.ResolveMoveControlPath(playerInput);
                
            // controlPath는 필요할 때만 갱신
            if (!string.Equals(stick.controlPath, targetPath, StringComparison.Ordinal))
            {
                // 이미 활성/초기화된 상태라면 controlPath만 변경하지 않음
                if (!stick.isActiveAndEnabled || stick.control == null)
                {
                    stick.controlPath = targetPath;
                }
            }

            // 아래 초기화는 항상 적용
            stick.movementRange = settings.joystickMovementRange;
            MobileOnScreenUtility.TryEnableIsolatedStickInput(stick);
        }

        private static void ApplyButtons(MobileHudRootView rootView, GGemCoMobileHudSettings settings, PlayerInput playerInput, MobileInputBindingResolver bindingResolver)
        {
            Vector2 buttonSize = settings.buttonSize;
            float pressedScale = settings.holdButtonPressedAlpha;

            ApplyButton(rootView.AttackButtonView, settings, settings.attackIcon, buttonSize, pressedScale,
                bindingResolver.ResolveButtonControlPath(playerInput, ConfigCommonControl.NameActionAttack, "<Gamepad>/buttonWest"));
            ApplyButton(rootView.JumpButtonView, settings, settings.jumpIcon, buttonSize, pressedScale,
                bindingResolver.ResolveButtonControlPath(playerInput, ConfigCommonControl.NameActionJump, "<Gamepad>/buttonSouth"));
            ApplyButton(rootView.DashButtonView, settings, settings.dashIcon, buttonSize, pressedScale,
                bindingResolver.ResolveButtonControlPath(playerInput, ConfigCommonControl.NameActionDash, "<Gamepad>/buttonEast"));
            ApplyButton(rootView.GuardButtonView, settings, settings.guardIcon, buttonSize * 0.92f, pressedScale,
                bindingResolver.ResolveButtonControlPath(playerInput, ConfigCommonControl.NameActionGuard, "<Gamepad>/leftShoulder"));
            ApplyButton(rootView.InteractionButtonView, settings, settings.interactionIcon, buttonSize * 0.82f, pressedScale,
                bindingResolver.ResolveButtonControlPath(playerInput, ConfigCommonControl.NameActionInteraction, "<Gamepad>/rightShoulder"));
        }

        private static void ApplyButton(MobileHudButtonView buttonView, GGemCoMobileHudSettings settings, Sprite iconSprite, Vector2 size, float pressedScale, string controlPath)
        {
            if (buttonView == null)
            {
                return;
            }

            buttonView.ApplyVisual(settings.buttonAlpha, settings.buttonBackgroundSprite != null ? settings.buttonBackgroundSprite : GetCircleSprite(), iconSprite, size, pressedScale);
            if (buttonView.OnScreenButton == null) return;
            
            // controlPath는 필요할 때만 갱신
            if (string.Equals(buttonView.OnScreenButton.controlPath, controlPath, StringComparison.Ordinal)) return;
            
            // 이미 활성/초기화된 상태라면 controlPath만 변경하지 않음
            if (!buttonView.OnScreenButton.isActiveAndEnabled || buttonView.OnScreenButton.control == null)
            {
                buttonView.OnScreenButton.controlPath = controlPath;
            }
        }

        private MobileHudRootView CreateRuntimeView(GGemCoMobileHudSettings settings)
        {
            GameObject root = new GameObject("MobileInputHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(MobileHudRootView));
            Object.DontDestroyOnLoad(root);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            RectTransform safeArea = CreateRect("SafeArea", root.transform as RectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safeArea.gameObject.AddComponent<MobileInputHudSafeAreaFitter>();

            RectTransform leftPanel = CreateRect("LeftPanel", safeArea, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(settings != null ? settings.joystickAreaSize : 300f, settings != null ? settings.joystickAreaSize : 300f));
            RectTransform rightPanel = CreateRect("RightPanel", safeArea, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(settings != null ? settings.actionAreaSize : 320f, settings != null ? settings.actionAreaSize : 320f));

            CreateJoystick(leftPanel);
            CreateButtons(rightPanel, settings);

            MobileHudRootView view = root.GetComponent<MobileHudRootView>();
            view.EnsureReferences();
            return view;
        }

        private static void CreateJoystick(RectTransform parent)
        {
            Image baseImage = CreateCircleImage("Joystick", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(176f, 176f), 0.55f);
            baseImage.gameObject.AddComponent<OnScreenStick>();
            baseImage.gameObject.AddComponent<MobileStickVisual>();
            baseImage.gameObject.AddComponent<MobileHudJoystickView>();
            CreateCircleImage("Knob", baseImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(82f, 82f), 0.88f);
        }

        private static void CreateButtons(RectTransform parent, GGemCoMobileHudSettings settings)
        {
            Vector2 buttonSize = settings != null ? settings.buttonSize : new Vector2(88f, 88f);
            CreateActionButton(parent, "AttackButton", new Vector2(-buttonSize.x * 0.65f, buttonSize.y * 0.55f), buttonSize);
            CreateActionButton(parent, "JumpButton", new Vector2(0f, buttonSize.y * 1.05f), buttonSize);
            CreateActionButton(parent, "DashButton", new Vector2(buttonSize.x * 0.85f, buttonSize.y * 0.6f), buttonSize);
            CreateActionButton(parent, "GuardButton", new Vector2(-buttonSize.x * 1.2f, -buttonSize.y * 0.1f), buttonSize * 0.92f);
            CreateActionButton(parent, "InteractionButton", new Vector2(buttonSize.x * 0.3f, -buttonSize.y * 0.35f), buttonSize * 0.82f);
        }

        private static void CreateActionButton(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            Image buttonImage = CreateCircleImage(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size, 0.68f);
            buttonImage.gameObject.AddComponent<OnScreenButton>();
            buttonImage.gameObject.AddComponent<MobileButtonVisual>();
            buttonImage.gameObject.AddComponent<MobileHudButtonView>();

            GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            icon.transform.SetParent(buttonImage.transform, false);
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = size * 0.55f;
            iconRect.anchoredPosition = Vector2.zero;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private static Image CreateCircleImage(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, float alpha)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Image image = go.GetComponent<Image>();
            image.sprite = GetCircleSprite();
            image.type = Image.Type.Simple;
            image.raycastTarget = true;
            image.color = new Color(1f, 1f, 1f, alpha);
            return image;
        }

        private static Sprite GetCircleSprite()
        {
            if (s_DefaultCircleSprite != null)
            {
                return s_DefaultCircleSprite;
            }

            const int textureSize = 128;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = "MobileInputHudCircle",
                hideFlags = HideFlags.HideAndDontSave
            };

            Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
            float radius = textureSize * 0.5f - 1f;
            Color clear = new Color(1f, 1f, 1f, 0f);
            Color solid = Color.white;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    texture.SetPixel(x, y, distance <= radius ? solid : clear);
                }
            }

            texture.Apply();
            s_DefaultCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            s_DefaultCircleSprite.name = "MobileInputHudCircleSprite";
            return s_DefaultCircleSprite;
        }
    }
}
