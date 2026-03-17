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
                var taskMobileHudSettings = LoadSettingsAsync<GGemCoMobileHudSettings>(ConfigAddressableSettingControl.MobileHudSettings.Key, optional: true);

                await Task.WhenAll(taskAttackCombo, taskPlayerSettings, taskMobileHudSettings);

                attackComboSettings = taskAttackCombo.Result;
                playerActionSettings = taskPlayerSettings.Result;
                mobileHudSettings = taskMobileHudSettings.Result;

                OnLoadSettings?.Invoke(attackComboSettings, playerActionSettings);
            }
            catch (Exception ex)
            {
                GcLogger.LogError($"설정 로딩 중 오류 발생: {ex.Message}");
            }
        }

        private async Task<T> LoadSettingsAsync<T>(string key, bool optional = false) where T : ScriptableObject
        {
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
