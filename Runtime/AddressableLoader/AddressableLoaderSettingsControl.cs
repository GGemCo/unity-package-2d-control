using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GGemCo2DControl
{
    /// <summary>
    /// Control 패키지 Settings를 Addressables에서 불러옵니다.
    /// </summary>
    public class AddressableLoaderSettingsControl : MonoBehaviour
    {
        public static AddressableLoaderSettingsControl Instance { get; private set; }

        [HideInInspector] public GGemCoAttackComboSettings attackComboSettings;
        [HideInInspector] public GGemCoPlayerActionSettings playerActionSettings;
        [HideInInspector] public GGemCoPlayerGuardSettings playerGuardSettings;
        [HideInInspector] public GGemCoMobileHudSettings mobileHudSettings;

        public delegate void DelegateLoadSettings(GGemCoAttackComboSettings attackComboSettings, GGemCoPlayerActionSettings playerActionSettings);
        public event DelegateLoadSettings OnLoadSettings;
        
        private readonly HashSet<AsyncOperationHandle> _activeHandles = new HashSet<AsyncOperationHandle>();
        private float _loadProgress;

        private void Awake()
        {
            _loadProgress = 0f;
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            ReleaseAll();
        }

        private void ReleaseAll()
        {
            AddressableLoaderController.ReleaseByHandles(_activeHandles);
        }

        public async Task LoadAllSettingsAsync()
        {
            try
            {
                var taskAttackCombo = LoadSettingsAsync<GGemCoAttackComboSettings>(ConfigAddressableSettingControl.AttackComboSettings.Key);
                var taskPlayerSettings = LoadSettingsAsync<GGemCoPlayerActionSettings>(ConfigAddressableSettingControl.PlayerActionSettings.Key);
                var taskPlayerGuardSettings = LoadSettingsAsync<GGemCoPlayerGuardSettings>(ConfigAddressableSettingControl.PlayerGuardSettings.Key);
                var taskMobileHudSettings = LoadSettingsAsync<GGemCoMobileHudSettings>(ConfigAddressableSettingControl.MobileHudSettings.Key, optional: true);

                await Task.WhenAll(taskAttackCombo, taskPlayerSettings, taskPlayerGuardSettings, taskMobileHudSettings);

                attackComboSettings = taskAttackCombo.Result;
                playerActionSettings = taskPlayerSettings.Result;
                playerGuardSettings = taskPlayerGuardSettings.Result;
                mobileHudSettings = taskMobileHudSettings.Result;

                OnLoadSettings?.Invoke(attackComboSettings, playerActionSettings);
            }
            catch (Exception ex)
            {
                GcLogger.LogError($"설정 로딩 중 오류 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// 개발용 Settings Override를 먼저 확인한 뒤, 없으면 Addressables에서 서비스용 Settings를 로드합니다.
        /// </summary>
        /// <typeparam name="T">로드할 ScriptableObject 타입입니다.</typeparam>
        /// <param name="key">서비스용 Settings Addressables Key입니다.</param>
        /// <param name="optional">true이면 Addressables에 등록되지 않아도 오류 로그를 출력하지 않습니다.</param>
        /// <returns>개발용 또는 서비스용 Settings 에셋입니다.</returns>
        private async Task<T> LoadSettingsAsync<T>(string key, bool optional = false) where T : ScriptableObject
        {
            // 에디터 Play Mode에서 작업자별 개발용 Settings가 등록되어 있으면 서비스용 Addressables보다 먼저 사용합니다.
            if (SettingsRuntimeResolver.TryGetOverride(key, out T overrideSettings))
            {
                return overrideSettings;
            }

            var locationsHandle = Addressables.LoadResourceLocationsAsync(key);
            await locationsHandle.Task;

            if (!locationsHandle.Status.Equals(AsyncOperationStatus.Succeeded) || locationsHandle.Result.Count == 0)
            {
                if (!optional)
                {
                    GcLogger.LogError($"[AddressableSettingsLoader] '{key}' 가 Addressables에 등록되지 않았습니다. '{key}' 를 생성한 후 {ConfigDefine.NameSDK}Tool > 기본 셋팅하기 메뉴를 열고 Addressable 추가하기 버튼을 클릭해주세요.");
                }

                Addressables.Release(locationsHandle);
                return null;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
            _activeHandles.Add(handle);
            T asset = await handle.Task;

            Addressables.Release(locationsHandle);
            return asset;
        }

        public float GetLoadProgress() => _loadProgress;
    }
}
