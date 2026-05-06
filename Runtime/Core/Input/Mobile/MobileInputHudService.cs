using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

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
        private bool _isRefreshing;
        private bool _hasPendingRefresh;
        private MobileInputHudCutsceneSuppressor _cutsceneSuppressor;

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
            _cutsceneSuppressor = new MobileInputHudCutsceneSuppressor(this);
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            BindCutsceneSuppressorToCurrentScene();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _cutsceneSuppressor?.Dispose();
            _cutsceneSuppressor = null;
            DestroyRootView();
            UnsubscribeSettings();
        }

        public void BindPlayer(PlayerInput playerInput)
        {
            if (playerInput == null)
            {
                return;
            }

            bool wasSamePlayer = _boundPlayerInput == playerInput;
            _boundPlayerInput = playerInput;
            ResolveSettings();

            if (wasSamePlayer && _rootView != null)
            {
                return;
            }

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
            if (_isRefreshing)
            {
                _hasPendingRefresh = true;
                return;
            }

            _isRefreshing = true;
            try
            {
                do
                {
                    _hasPendingRefresh = false;
                    RefreshInternal();
                }
                while (_hasPendingRefresh);
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void RefreshInternal()
        {
            BindCutsceneSuppressorToCurrentScene();
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

        /// <summary>
        /// 지정한 사유로 모바일 HUD 표시를 억제하거나 억제를 해제합니다.
        /// 여러 사유가 동시에 등록될 수 있으며 모든 사유가 해제되어야 HUD가 다시 표시됩니다.
        /// </summary>
        /// <param name="reason">표시 억제 사유를 식별하는 문자열입니다.</param>
        /// <param name="suppressed">해당 사유로 숨기려면 true, 해제하려면 false입니다.</param>
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
            BindCutsceneSuppressorToCurrentScene();
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

        /// <summary>
        /// 현재 SceneGame의 CutsceneManager를 모바일 HUD 컷신 억제 브리지에 연결합니다.
        /// 씬 로드 직후 또는 서비스 생성 직후 호출되어 컷신 진행 상태와 조이스틱 표시 상태를 동기화합니다.
        /// </summary>
        private void BindCutsceneSuppressorToCurrentScene()
        {
            CutsceneManager cutsceneManager = SceneGame.Instance != null
                ? SceneGame.Instance.CutsceneManager
                : null;

            _cutsceneSuppressor?.Bind(cutsceneManager);
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
