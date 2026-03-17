using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// 모바일 HUD 생성, 재사용, 플레이어 재바인딩을 관리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileInputHudService : MonoBehaviour
    {
        public static MobileInputHudService Instance { get; private set; }

        private readonly MobileInputHudPresenter _presenter = new MobileInputHudPresenter();
        private readonly MobileInputBindingResolver _bindingResolver = new MobileInputBindingResolver();
        private readonly MobileInputHudVisibilityService _visibilityService = new MobileInputHudVisibilityService();

        private MobileHudRootView _rootView;
        private PlayerInput _boundPlayerInput;
        private GGemCoMobileHudSettings _settings;
        private GGemCoMobileHudSettings _runtimeDefaultSettings;

        public static MobileInputHudService EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            GameObject host = new GameObject(nameof(MobileInputHudService));
            DontDestroyOnLoad(host);
            return host.AddComponent<MobileInputHudService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            UnsubscribeSettings();
        }

        public void BindPlayer(PlayerInput playerInput)
        {
            if (playerInput == null)
            {
                return;
            }

            _boundPlayerInput = playerInput;
            ResolveSettings();
            Refresh();
        }

        public void UnbindPlayer(PlayerInput playerInput)
        {
            if (_boundPlayerInput != null && playerInput != null && _boundPlayerInput != playerInput)
            {
                return;
            }

            _boundPlayerInput = null;
            _visibilityService.SetVisibleByPlatform(false);
        }

        public void Refresh()
        {
            ResolveSettings();
            bool shouldShow = ShouldEnableHud();
            _visibilityService.SetVisibleByPlatform(shouldShow);

            if (!shouldShow || _boundPlayerInput == null)
            {
                return;
            }

            _presenter.EnsureEventSystem();

            if (_rootView == null)
            {
                _rootView = _presenter.CreateOrInstantiateView(_settings);
            }

            _presenter.Apply(_rootView, _settings, _boundPlayerInput, _bindingResolver);
            _visibilityService.Bind(_rootView.RootCanvasGroup);
        }

        public void SetSuppressed(string reason, bool suppressed)
        {
            _visibilityService.SetSuppressed(reason, suppressed);
        }

        private void ResolveSettings()
        {
            GGemCoMobileHudSettings next = AddressableLoaderSettingsControl.Instance != null
                ? AddressableLoaderSettingsControl.Instance.mobileHudSettings
                : null;

            if (next == null)
            {
                if (_runtimeDefaultSettings == null)
                {
                    _runtimeDefaultSettings = ScriptableObject.CreateInstance<GGemCoMobileHudSettings>();
                    _runtimeDefaultSettings.name = nameof(GGemCoMobileHudSettings) + "(RuntimeDefault)";
                }

                next = _runtimeDefaultSettings;
            }

            if (_settings == next)
            {
                return;
            }

            UnsubscribeSettings();
            _settings = next;
            _settings.Changed += OnSettingsChanged;
        }

        private void UnsubscribeSettings()
        {
            if (_settings != null)
            {
                _settings.Changed -= OnSettingsChanged;
            }
        }

        private void OnSettingsChanged()
        {
            Refresh();
        }

        private bool ShouldEnableHud()
        {
            if (_settings == null || !_settings.enableMobileHud)
            {
                return false;
            }

#if UNITY_EDITOR
            if (_settings.forceShowInEditor)
            {
                return true;
            }
#endif

            if (!_settings.enableOnMobilePlatformOnly)
            {
                return true;
            }

            if (!Application.isMobilePlatform && SystemInfo.deviceType != DeviceType.Handheld)
            {
                return false;
            }

            if (!_settings.autoHideWhenNonTouchScheme || _boundPlayerInput == null)
            {
                return true;
            }

            string scheme = _boundPlayerInput.currentControlScheme;
            return string.IsNullOrWhiteSpace(scheme) || scheme == ConfigCommonControl.NameControlSchemeTouch || scheme == ConfigCommonControl.NameControlSchemeGamepad;
        }
    }
}
