using UnityEngine;
using GGemCo2DCore;

namespace GGemCo2DControl
{
    /// <summary>
    /// 게임 시작 시 Control 패키지에서 필요한 리소스 로딩 스텝을 GameLoaderManager에 등록
    /// </summary>
    public class GameLoaderManagerControl : MonoBehaviour
    {
        /*
         * (다양한 로우 레벨 시스템(윈도우, 어셈블리, Gfx 등) 초기화)
            1. SubsystemRegistration, AfterAssembliesLoaded
            (입력 시스템 등 초기화)
            2. BeforeSplashScreen
            (첫 번째 씬 로드)
            3. BeforeSceneLoad
            (모든 오브젝트를 활성화. MonoBehaviour의 Awake, OnEnable 호출)
            4. AfterSceneLoad
            (Start 호출)
         */
        
        private void OnEnable()
        {
            GameLoaderManager.BeforeLoadStart += OnBeforeLoadStart;
        }
        private void OnDisable()
        {
            GameLoaderManager.BeforeLoadStart -= OnBeforeLoadStart;
        }

        private void OnBeforeLoadStart(GameLoaderManager sender, GameLoaderManager.EventArgsBeforeLoadStart e)
        {
            // GcLogger.Log($"GameLoaderManagerControl RegisterSteps");
            // 설정 스크립터블 오브젝트 
            var addrSettings = Object.FindFirstObjectByType<AddressableLoaderSettingsControl>() ??
                               new GameObject("AddressableLoaderSettingsControl")
                                   .AddComponent<AddressableLoaderSettingsControl>();
            var step = new AddressableTaskStep(
                id: "control.settings",
                order: 240,
                localizedKey: LocalizationConstants.Keys.Loading.TextTypeSettings(),
                startTask: () => addrSettings.LoadAllSettingsAsync(),
                getProgress: () => addrSettings.GetLoadProgress()
            );
            sender.Register(step);

            // Input Action Asset
            var addressableLoaderInputAction = Object.FindFirstObjectByType<AddressableLoaderInputAction>() ??
                                               new GameObject("AddressableLoaderInputAction")
                                                   .AddComponent<AddressableLoaderInputAction>();
            step = new AddressableTaskStep(
                id: "control.inputaction",
                order: 250,
                localizedKey: LocalizationConstants.Keys.Loading.TextTypeInputAction(),
                startTask: () =>
                    addressableLoaderInputAction.LoadPrefabsAsync(ConfigAddressableControl.InputAction.Label),
                getProgress: () => addressableLoaderInputAction.GetPrefabLoadProgress()
            );
            sender.Register(step);
        }
    }
}