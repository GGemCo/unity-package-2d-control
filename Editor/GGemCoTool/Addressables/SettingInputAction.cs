using System.IO;
using GGemCo2DControl;
using GGemCo2DCore;
using GGemCo2DCoreEditor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace GGemCo2DControlEditor
{
    /// <summary>
    /// 테이블 등록하기
    /// </summary>
    public class SettingInputAction : DefaultAddressable
    {
        private const string Title = "Input Action 추가하기";
        private readonly AddressableEditorControl _addressableEditorControl;

        public SettingInputAction(AddressableEditorControl addressableEditorControlWindow)
        {
            _addressableEditorControl = addressableEditorControlWindow;
            TargetGroupName = $"{ConfigAddressableGroupName.InputAction}";
        }
        public void OnGUI()
        {
            // Common.OnGUITitle(Title);
            var path = ConfigAddressableControl.InputAction.Path;
            if (!File.Exists(path))
            {
                EditorGUILayout.HelpBox($"Input Action 파일이 없습니다.", MessageType.Info);
            }
            else
            {
                if (GUILayout.Button(Title, GUILayout.Width(_addressableEditorControl.buttonWidth), GUILayout.Height(_addressableEditorControl.buttonHeight)))
                {
                    Setup();
                }
            }
        }
        
        /// <summary>
        /// Addressable 설정하기
        /// </summary>
        private void Setup()
        {
            // AddressableSettings 가져오기 (없으면 생성)
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (!settings)
            {
                Debug.LogWarning("Addressable 설정을 찾을 수 없습니다. 새로 생성합니다.");
                settings = CreateAddressableSettings();
            }

            // GGemCo_Tables 그룹 가져오기 또는 생성
            AddressableAssetGroup groupMonster = GetOrCreateGroup(settings, TargetGroupName);

            string key = ConfigAddressableControl.InputAction.Key;
            string assetPath = ConfigAddressableControl.InputAction.Path;
            string label = ConfigAddressableControl.InputAction.Label;
        
            Add(settings, groupMonster, key, assetPath, label);

            // 설정 저장
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
            AssetDatabase.SaveAssets();
            
            EditorUtility.DisplayDialog(Title, "Addressable 설정 완료", "OK");
        }

    }
}