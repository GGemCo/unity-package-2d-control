using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
        private GameObject _viewPrefabSource;
        private bool _hasLoggedMissingSettings;
        private bool _hasLoggedMissingPrefab;
        private bool _hasLoggedMissingRootView;

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
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            DestroyRootView();
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
            DestroyRootView();
        }

        public void Refresh()
        {
            ResolveSettings();
            ValidateRootViewState();
            bool shouldShow = ShouldEnableHud();
            _visibilityService.SetVisibleByPlatform(shouldShow);

            if (!shouldShow || _boundPlayerInput == null)
            {
                return;
            }

            if (_settings == null)
            {
                LogMissingSettings();
                DestroyRootView();
                return;
            }

            if (_settings.hudPrefab == null)
            {
                LogMissingPrefab();
                DestroyRootView();
                return;
            }

            _presenter.EnsureEventSystem();

            if (_rootView == null)
            {
                _rootView = _presenter.CreateOrInstantiateView(_settings);
                _viewPrefabSource = _settings.hudPrefab;
                if (_rootView == null)
                {
                    LogMissingRootView();
                    return;
                }
            }

            _presenter.Apply(_rootView, _settings, _boundPlayerInput, _bindingResolver);
            if (_rootView.RootCanvasGroup != null)
            {
                _visibilityService.Bind(_rootView.RootCanvasGroup);
            }
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

            if (_settings == next)
            {
                return;
            }

            UnsubscribeSettings();
            _settings = next;
            if (_settings != null)
            {
                _settings.Changed += OnSettingsChanged;
            }
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
            ResetWarnings();
            ValidateRootViewState(forceRecreate: true);
            Refresh();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ValidateRootViewState(forceRecreate: true);
            Refresh();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (_rootView == null)
            {
                return;
            }

            if (_rootView.gameObject.scene == scene)
            {
                _rootView = null;
                _viewPrefabSource = null;
                _visibilityService.Bind(null);
            }
        }

        private void ValidateRootViewState(bool forceRecreate = false)
        {
            bool shouldDestroy = forceRecreate;

            if (!shouldDestroy && _rootView == null)
            {
                return;
            }

            if (!shouldDestroy && !_rootView)
            {
                shouldDestroy = true;
            }

            if (!shouldDestroy && _rootView.gameObject.scene != SceneManager.GetActiveScene())
            {
                shouldDestroy = true;
            }

            if (!shouldDestroy && _settings != null && _settings.hudPrefab != _viewPrefabSource)
            {
                shouldDestroy = true;
            }

            if (!shouldDestroy)
            {
                return;
            }

            DestroyRootView();
        }

        private void DestroyRootView()
        {
            _visibilityService.Bind(null);

            if (_rootView != null)
            {
                Destroy(_rootView.gameObject);
            }

            _rootView = null;
            _viewPrefabSource = null;
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

        private void ResetWarnings()
        {
            _hasLoggedMissingSettings = false;
            _hasLoggedMissingPrefab = false;
            _hasLoggedMissingRootView = false;
        }

        private void LogMissingSettings()
        {
            if (_hasLoggedMissingSettings)
            {
                return;
            }

            _hasLoggedMissingSettings = true;
            Debug.LogWarning("[MobileInputHudService] GGemCoMobileHudSettings 가 로드되지 않아 모바일 HUD 생성을 건너뜁니다.");
        }

        private void LogMissingPrefab()
        {
            if (_hasLoggedMissingPrefab)
            {
                return;
            }

            _hasLoggedMissingPrefab = true;
            Debug.LogWarning("[MobileInputHudService] hudPrefab 이 지정되지 않아 모바일 HUD 생성을 건너뜁니다.");
        }

        private void LogMissingRootView()
        {
            if (_hasLoggedMissingRootView)
            {
                return;
            }

            _hasLoggedMissingRootView = true;
            Debug.LogWarning("[MobileInputHudService] hudPrefab 인스턴스에서 MobileHudRootView 를 찾지 못해 모바일 HUD 생성을 건너뜁니다.");
        }
    }
}
