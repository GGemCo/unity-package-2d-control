using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GGemCo2DControl
{
    /// <summary>
    /// 모바일/터치 디바이스에서 사용할 온스크린 조이스틱과 액션 버튼 HUD를 런타임에 생성합니다.
    /// 기존 InputAction을 직접 호출하지 않고, Input System의 On-Screen Control을 통해 기존 PlayerInput 액션 파이프를 재사용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileInputHudRuntime : MonoBehaviour
    {
        private const float DefaultBottomPadding = 28f;
        private const float DefaultSidePadding = 24f;
        private const float DefaultJoystickAreaSize = 300f;
        private const float DefaultActionAreaSize = 320f;
        private const float DefaultButtonSize = 88f;
        private const float DefaultStickBaseSize = 176f;
        private const float DefaultStickKnobSize = 82f;
        private const float DefaultStickMovementRange = 68f;

        [Header("실행 조건")]
        [SerializeField] private bool enableOnMobilePlatformOnly = true;
        [SerializeField] private bool forceEnableInEditor = true;

        [Header("배치")]
        [SerializeField] private float bottomPadding = DefaultBottomPadding;
        [SerializeField] private float sidePadding = DefaultSidePadding;
        [SerializeField] private float joystickAreaSize = DefaultJoystickAreaSize;
        [SerializeField] private float actionAreaSize = DefaultActionAreaSize;
        [SerializeField] private float buttonSize = DefaultButtonSize;
        [SerializeField] private float stickBaseSize = DefaultStickBaseSize;
        [SerializeField] private float stickKnobSize = DefaultStickKnobSize;
        [SerializeField] private float stickMovementRange = DefaultStickMovementRange;
        [SerializeField] private int canvasSortingOrder = 500;

        [Header("표시")]
        [SerializeField, Range(0.05f, 1f)] private float panelAlpha = 0.55f;
        [SerializeField, Range(0.05f, 1f)] private float buttonAlpha = 0.68f;
        [SerializeField, Range(0.05f, 1f)] private float knobAlpha = 0.88f;

        private PlayerInput _playerInput;
        private GameObject _root;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            if (_playerInput == null)
            {
                _playerInput = GetComponentInChildren<PlayerInput>(true);
            }

            if (_playerInput == null)
            {
                enabled = false;
                return;
            }

            if (!ShouldCreateHud())
            {
                enabled = false;
                return;
            }

            EnsureEventSystem();
            CreateHud();
        }

        private void OnEnable()
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_root != null)
            {
                Destroy(_root);
                _root = null;
            }
        }

        /// <summary>
        /// 외부 시스템(컷씬/대화창/옵션창 등)에서 모바일 컨트롤 표시 여부를 제어할 때 사용합니다.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_canvasGroup == null) return;

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }

        private bool ShouldCreateHud()
        {
#if UNITY_EDITOR
            if (forceEnableInEditor)
            {
                return true;
            }
#endif
            if (!enableOnMobilePlatformOnly)
            {
                return true;
            }

            return Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                if (EventSystem.current.GetComponent<InputSystemUIInputModule>() == null)
                {
                    EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                return;
            }

            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(eventSystemGo);
        }

        private void CreateHud()
        {
            if (_root != null)
            {
                return;
            }

            _root = new GameObject("MobileInputHud");
            DontDestroyOnLoad(_root);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;

            _root.AddComponent<GraphicRaycaster>();

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            _canvasGroup = _root.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;

            var safeArea = new GameObject("SafeArea", typeof(RectTransform), typeof(MobileSafeAreaFitter));
            safeArea.transform.SetParent(_root.transform, false);
            var safeAreaRect = safeArea.GetComponent<RectTransform>();
            safeAreaRect.anchorMin = Vector2.zero;
            safeAreaRect.anchorMax = Vector2.one;
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;

            CreateJoystickPanel(safeAreaRect);
            CreateActionButtonsPanel(safeAreaRect);
        }

        private void CreateJoystickPanel(RectTransform parent)
        {
            var area = CreateRect("JoystickArea", parent, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(sidePadding, bottomPadding), new Vector2(joystickAreaSize, joystickAreaSize));

            var baseImage = CreateCircleImage("JoystickBase", area, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(stickBaseSize, stickBaseSize), panelAlpha);
            var stick = baseImage.gameObject.AddComponent<OnScreenStick>();
            stick.controlPath = ResolveMoveControlPath();
            stick.movementRange = stickMovementRange;
            TryEnableIsolatedStickInput(stick);

            var knob = CreateCircleImage("JoystickKnob", baseImage.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(stickKnobSize, stickKnobSize), knobAlpha);

            var stickVisual = baseImage.gameObject.AddComponent<MobileStickVisual>();
            stickVisual.Initialize(baseImage.rectTransform, knob.rectTransform, stickMovementRange);
        }

        private void CreateActionButtonsPanel(RectTransform parent)
        {
            var area = CreateRect("ActionButtonsArea", parent, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-sidePadding, bottomPadding), new Vector2(actionAreaSize, actionAreaSize));

            CreateActionButton(area, "AttackButton", ResolveButtonControlPath(ConfigCommonControl.NameActionAttack, "<Gamepad>/buttonWest"),
                new Vector2(-buttonSize * 0.65f, buttonSize * 0.55f), buttonSize);
            CreateActionButton(area, "JumpButton", ResolveButtonControlPath(ConfigCommonControl.NameActionJump, "<Gamepad>/buttonSouth"),
                new Vector2(0f, buttonSize * 1.05f), buttonSize);
            CreateActionButton(area, "DashButton", ResolveButtonControlPath(ConfigCommonControl.NameActionDash, "<Gamepad>/buttonEast"),
                new Vector2(buttonSize * 0.85f, buttonSize * 0.6f), buttonSize);
            CreateActionButton(area, "GuardButton", ResolveButtonControlPath(ConfigCommonControl.NameActionGuard, "<Gamepad>/leftShoulder"),
                new Vector2(-buttonSize * 1.2f, -buttonSize * 0.1f), buttonSize * 0.92f);
            CreateActionButton(area, "InteractionButton", ResolveButtonControlPath(ConfigCommonControl.NameActionInteraction, "<Gamepad>/rightShoulder"),
                new Vector2(buttonSize * 0.3f, -buttonSize * 0.35f), buttonSize * 0.82f);
        }

        private void CreateActionButton(RectTransform parent, string name, string controlPath, Vector2 anchoredPosition, float size)
        {
            var buttonImage = CreateCircleImage(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                anchoredPosition, new Vector2(size, size), buttonAlpha);

            var onScreenButton = buttonImage.gameObject.AddComponent<OnScreenButton>();
            onScreenButton.controlPath = controlPath;

            var feedback = buttonImage.gameObject.AddComponent<MobileButtonVisual>();
            feedback.Initialize(buttonImage.rectTransform);
        }

        private string ResolveMoveControlPath()
        {
            var moveAction = _playerInput.actions?.FindAction(ConfigCommonControl.NameActionMove);
            string path = TryGetFirstGamepadBindingPath(moveAction);
            return string.IsNullOrWhiteSpace(path) ? "<Gamepad>/leftStick" : path;
        }

        private string ResolveButtonControlPath(string actionName, string fallback)
        {
            var action = _playerInput.actions?.FindAction(actionName);
            string path = TryGetFirstGamepadBindingPath(action);
            return string.IsNullOrWhiteSpace(path) ? fallback : path;
        }

        private static string TryGetFirstGamepadBindingPath(InputAction action)
        {
            if (action == null)
            {
                return null;
            }

            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.isComposite || binding.isPartOfComposite)
                {
                    continue;
                }

                string path = binding.effectivePath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = binding.path;
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (path.IndexOf("<Gamepad>", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return path;
                }
            }

            return null;
        }

        private static void TryEnableIsolatedStickInput(OnScreenStick stick)
        {
            if (stick == null)
            {
                return;
            }

            const string propertyName = "useIsolatedInputActions";
            PropertyInfo property = typeof(OnScreenStick).GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property is { CanWrite: true } && property.PropertyType == typeof(bool))
            {
                property.SetValue(stick, true);
            }
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private static Image CreateCircleImage(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta, float alpha)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var image = go.GetComponent<Image>();
            image.sprite = CreateCircleSprite();
            image.type = Image.Type.Simple;
            image.raycastTarget = true;
            image.color = new Color(1f, 1f, 1f, alpha);
            return image;
        }

        private static Sprite CreateCircleSprite()
        {
            if (s_CircleSprite != null)
            {
                return s_CircleSprite;
            }

            const int textureSize = 128;
            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
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
            s_CircleSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            s_CircleSprite.name = "MobileInputHudCircleSprite";
            return s_CircleSprite;
        }

        private static Sprite s_CircleSprite;
    }

    /// <summary>
    /// 디바이스 회전/노치 변경 시 Safe Area에 맞춰 모바일 컨트롤 영역을 재배치합니다.
    /// </summary>
    internal sealed class MobileSafeAreaFitter : MonoBehaviour
    {
        private Rect _lastSafeArea = Rect.zero;
        private RectTransform _rectTransform;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (_rectTransform == null)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            Vector2 min = safeArea.position;
            Vector2 max = safeArea.position + safeArea.size;

            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rectTransform.anchorMin = min;
            _rectTransform.anchorMax = max;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
            _lastSafeArea = safeArea;
        }
    }

    /// <summary>
    /// OnScreenStick 자체는 입력 생성만 담당하므로, 조이스틱 노브 시각 이동은 별도 컴포넌트에서 처리합니다.
    /// </summary>
    internal sealed class MobileStickVisual : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private RectTransform _baseRect;
        private RectTransform _knobRect;
        private float _movementRange;

        public void Initialize(RectTransform baseRect, RectTransform knobRect, float movementRange)
        {
            _baseRect = baseRect;
            _knobRect = knobRect;
            _movementRange = Mathf.Max(1f, movementRange);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            UpdateKnobPosition(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateKnobPosition(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_knobRect != null)
            {
                _knobRect.anchoredPosition = Vector2.zero;
            }
        }

        private void UpdateKnobPosition(PointerEventData eventData)
        {
            if (_baseRect == null || _knobRect == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_baseRect, eventData.position,
                    eventData.pressEventCamera, out Vector2 localPoint))
            {
                return;
            }

            Vector2 clamped = Vector2.ClampMagnitude(localPoint, _movementRange);
            _knobRect.anchoredPosition = clamped;
        }
    }

    /// <summary>
    /// 버튼이 눌린 상태를 시각적으로 확인하기 위한 간단한 스케일 피드백입니다.
    /// </summary>
    internal sealed class MobileButtonVisual : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform _rectTransform;
        private Vector3 _defaultScale;

        public void Initialize(RectTransform rectTransform)
        {
            _rectTransform = rectTransform;
            _defaultScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_rectTransform != null)
            {
                _rectTransform.localScale = _defaultScale * 0.92f;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_rectTransform != null)
            {
                _rectTransform.localScale = _defaultScale;
            }
        }
    }
}
