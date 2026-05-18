using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace GGemCo2DControl
{
    /// <summary>
    /// 모바일 HUD 루트에서 필요한 참조를 모으고 보정하는 뷰입니다.
    /// </summary>
    public sealed class MobileHudRootView : MonoBehaviour
    {
        private const string LeftCombatZoneName = "LeftCombatZone";
        private const string RightCombatZoneName = "RightCombatZone";

        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private RectTransform safeAreaRoot;
        [SerializeField] private RectTransform leftPanel;
        [SerializeField] private RectTransform rightPanel;
        [SerializeField] private MobileHudJoystickView joystickView;
        [SerializeField] private MobileHudButtonView attackButtonView;
        [SerializeField] private MobileHudButtonView jumpButtonView;
        [SerializeField] private MobileHudButtonView dashButtonView;
        [SerializeField] private MobileHudButtonView guardButtonView;
        [SerializeField] private MobileHudButtonView interactionButtonView;
        [SerializeField] private MobileHudButtonView leftCombatZoneView;
        [SerializeField] private MobileHudButtonView rightCombatZoneView;
        [SerializeField] private MobileInputHudSafeAreaFitter safeAreaFitter;

        public Canvas Canvas => canvas;
        public CanvasGroup RootCanvasGroup => rootCanvasGroup;
        public RectTransform SafeAreaRoot => safeAreaRoot;
        public RectTransform LeftPanel => leftPanel;
        public RectTransform RightPanel => rightPanel;
        public MobileHudJoystickView JoystickView => joystickView;
        public MobileHudButtonView AttackButtonView => attackButtonView;
        public MobileHudButtonView JumpButtonView => jumpButtonView;
        public MobileHudButtonView DashButtonView => dashButtonView;
        public MobileHudButtonView GuardButtonView => guardButtonView;
        public MobileHudButtonView InteractionButtonView => interactionButtonView;
        public MobileHudButtonView LeftCombatZoneView => leftCombatZoneView;
        public MobileHudButtonView RightCombatZoneView => rightCombatZoneView;
        public MobileInputHudSafeAreaFitter SafeAreaFitter => safeAreaFitter;

        /// <summary>
        /// HUD 루트의 필수 참조를 탐색하고 누락된 터치 존까지 보정합니다.
        /// </summary>
        public void EnsureReferences()
        {
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (rootCanvasGroup == null) rootCanvasGroup = GetComponent<CanvasGroup>();
            if (safeAreaRoot == null) safeAreaRoot = transform.Find("SafeArea") as RectTransform;
            if (leftPanel == null) leftPanel = safeAreaRoot != null ? safeAreaRoot.Find("LeftPanel") as RectTransform : null;
            if (rightPanel == null) rightPanel = safeAreaRoot != null ? safeAreaRoot.Find("RightPanel") as RectTransform : null;
            if (safeAreaFitter == null) safeAreaFitter = safeAreaRoot != null ? safeAreaRoot.GetComponent<MobileInputHudSafeAreaFitter>() : null;

            EnsureCombatZoneViews();

            if (joystickView == null) joystickView = leftPanel != null ? leftPanel.GetComponentInChildren<MobileHudJoystickView>(true) : null;
            if (attackButtonView == null) attackButtonView = FindButton(rightPanel, "AttackButton");
            if (jumpButtonView == null) jumpButtonView = FindButton(rightPanel, "JumpButton");
            if (dashButtonView == null) dashButtonView = FindButton(rightPanel, "DashButton");
            if (guardButtonView == null) guardButtonView = FindButton(rightPanel, "GuardButton");
            if (interactionButtonView == null) interactionButtonView = FindButton(rightPanel, "InteractionButton");

            joystickView?.EnsureReferences();
            attackButtonView?.EnsureReferences();
            jumpButtonView?.EnsureReferences();
            dashButtonView?.EnsureReferences();
            guardButtonView?.EnsureReferences();
            interactionButtonView?.EnsureReferences();
            leftCombatZoneView?.EnsureReferences();
            rightCombatZoneView?.EnsureReferences();
        }

        /// <summary>
        /// 화면 좌/우 반분할 전투 입력용 터치 존을 찾거나 생성합니다.
        /// </summary>
        public void EnsureCombatZoneViews()
        {
            if (safeAreaRoot == null)
            {
                return;
            }

            leftCombatZoneView = EnsureCombatZoneView(leftCombatZoneView, LeftCombatZoneName, new Vector2(0f, 0f), new Vector2(0.5f, 1f));
            rightCombatZoneView = EnsureCombatZoneView(rightCombatZoneView, RightCombatZoneName, new Vector2(0.5f, 0f), new Vector2(1f, 1f));
        }

        /// <summary>
        /// 지정한 전투 존 오브젝트를 보정하고 버튼 뷰를 반환합니다.
        /// </summary>
        /// <param name="current">현재 캐시된 뷰입니다.</param>
        /// <param name="zoneName">전투 존 이름입니다.</param>
        /// <param name="anchorMin">RectTransform 최소 앵커입니다.</param>
        /// <param name="anchorMax">RectTransform 최대 앵커입니다.</param>
        /// <returns>보정이 완료된 전투 존 뷰입니다.</returns>
        private MobileHudButtonView EnsureCombatZoneView(MobileHudButtonView current, string zoneName, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (current != null)
            {
                current.EnsureReferences();
                return current;
            }

            Transform found = safeAreaRoot.Find(zoneName);
            GameObject zoneObject;
            if (found != null)
            {
                zoneObject = found.gameObject;
            }
            else
            {
                zoneObject = new GameObject(zoneName);
                zoneObject.transform.SetParent(safeAreaRoot, false);
            }

            RectTransform rectTransform = GetOrAddComponent<RectTransform>(zoneObject);
            ConfigureZoneRect(rectTransform, anchorMin, anchorMax);

            Image image = GetOrAddComponent<Image>(zoneObject);
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            GetOrAddComponent<OnScreenButton>(zoneObject);

            MobileHudButtonView view = GetOrAddComponent<MobileHudButtonView>(zoneObject);
            view.EnsureReferences();
            return view;
        }

        /// <summary>
        /// 전투 존이 지정된 절반 영역을 정확히 덮도록 RectTransform을 설정합니다.
        /// </summary>
        /// <param name="rectTransform">설정 대상 RectTransform입니다.</param>
        /// <param name="anchorMin">최소 앵커입니다.</param>
        /// <param name="anchorMax">최대 앵커입니다.</param>
        private static void ConfigureZoneRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // 기존 HUD 버튼보다 뒤 레이어로 배치하여 겹칠 때 우선순위를 보존합니다.
            rectTransform.SetSiblingIndex(0);
        }

        /// <summary>
        /// 대상 오브젝트에 컴포넌트가 없으면 추가하고 반환합니다.
        /// </summary>
        /// <typeparam name="T">확보할 컴포넌트 타입입니다.</typeparam>
        /// <param name="target">컴포넌트를 확보할 대상 오브젝트입니다.</param>
        /// <returns>확보된 컴포넌트입니다.</returns>
        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return target.AddComponent<T>();
        }

        /// <summary>
        /// 부모 하위에서 지정 이름의 버튼 뷰를 찾습니다.
        /// </summary>
        /// <param name="parent">탐색 기준 부모입니다.</param>
        /// <param name="name">탐색할 자식 이름입니다.</param>
        /// <returns>찾은 버튼 뷰, 없으면 null 입니다.</returns>
        private static MobileHudButtonView FindButton(RectTransform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            Transform child = parent.Find(name);
            return child != null ? child.GetComponent<MobileHudButtonView>() : null;
        }
    }
}
