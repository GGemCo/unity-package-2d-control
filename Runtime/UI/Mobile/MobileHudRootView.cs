using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 모바일 HUD 프리팹 루트에서 필요한 참조를 모아두는 뷰입니다.
    /// </summary>
    public sealed class MobileHudRootView : MonoBehaviour
    {
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
        public MobileInputHudSafeAreaFitter SafeAreaFitter => safeAreaFitter;

        public void EnsureReferences()
        {
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (rootCanvasGroup == null) rootCanvasGroup = GetComponent<CanvasGroup>();
            if (safeAreaRoot == null) safeAreaRoot = transform.Find("SafeArea") as RectTransform;
            if (leftPanel == null) leftPanel = safeAreaRoot != null ? safeAreaRoot.Find("LeftPanel") as RectTransform : null;
            if (rightPanel == null) rightPanel = safeAreaRoot != null ? safeAreaRoot.Find("RightPanel") as RectTransform : null;
            if (safeAreaFitter == null) safeAreaFitter = safeAreaRoot != null ? safeAreaRoot.GetComponent<MobileInputHudSafeAreaFitter>() : null;

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
        }

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
