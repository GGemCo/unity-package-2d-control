using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GGemCo2DCore
{
    /// <summary>
    /// 사운드 로드
    /// </summary>
    public class AddressableLoaderInputAction : MonoBehaviour
    {
        public static AddressableLoaderInputAction Instance { get; private set; }
        private readonly Dictionary<string, InputActionAsset> _dicInputAction = new Dictionary<string, InputActionAsset>();
        private readonly HashSet<AsyncOperationHandle> _activeHandles = new HashSet<AsyncOperationHandle>();
        private float _prefabLoadProgress;

        private void Awake()
        {
            _prefabLoadProgress = 0f;
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

        /// <summary>
        /// 모든 로드된 리소스를 해제합니다.
        /// </summary>
        private void ReleaseAll()
        {
            AddressableLoaderController.ReleaseByHandles(_activeHandles);
        }

        public async Task LoadPrefabsAsync(string label)
        {
            try
            {
                if (string.IsNullOrEmpty(label)) return;
                
                // 아이콘 이미지
                _dicInputAction.Clear();
                
                var locationHandle = Addressables.LoadResourceLocationsAsync(label);
                await locationHandle.Task;

                if (!locationHandle.IsValid() || locationHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    GcLogger.LogError($"{label} 레이블을 가진 리소스를 찾을 수 없습니다.");
                    return;
                }

                int totalCount = locationHandle.Result.Count;
                int loadedCount = 0;

                foreach (var location in locationHandle.Result)
                {
                    string address = location.PrimaryKey;
                    var loadHandle = Addressables.LoadAssetAsync<InputActionAsset>(address);

                    while (!loadHandle.IsDone)
                    {
                        _prefabLoadProgress = (loadedCount + loadHandle.PercentComplete) / totalCount;
                        await Task.Yield();
                    }
                    _activeHandles.Add(loadHandle);

                    InputActionAsset prefab = await loadHandle.Task;
                    if (prefab == null) continue;
                    _dicInputAction[address] = prefab;
                    loadedCount++;
                }
                _activeHandles.Add(locationHandle);

                _prefabLoadProgress = 1f; // 100%
                // GcLogger.Log($"총 {loadedCount}/{totalCount}개의 프리팹을 성공적으로 로드했습니다.");
            }
            catch (Exception ex)
            {
                GcLogger.LogError($"사운드 로딩 중 오류 발생: {ex.Message}");
            }
        }

        public InputActionAsset GetInputAction(string keyName)
        {
            if (_dicInputAction.TryGetValue(keyName, out var inputAction))
            {
                return inputAction;
            }

            GcLogger.LogError($"Addressables에서 {keyName} InputAction을 찾을 수 없습니다.");
            return null;
        }
        public float GetPrefabLoadProgress() => _prefabLoadProgress;
    }
}
