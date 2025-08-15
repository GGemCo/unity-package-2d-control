using System;
using System.IO;
using GGemCo2DControl;
using GGemCo2DCore;
using GGemCo2DCoreEditor;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DControlEditor
{
    public class SettingGGemCoControl
    {
        private const string Title = "설정 ScriptableObject 추가하기";
        private const string SettingsFolder = "Assets/"+ConfigDefine.NameSDK+"/Settings/";
        private readonly DefaultSettingsToolControl _defaultSettingsToolControl;
        public SettingGGemCoControl(DefaultSettingsToolControl defaultSettingsToolControl)
        {
            _defaultSettingsToolControl = defaultSettingsToolControl;
        }
        public void OnGUI()
        {
            // Common.OnGUITitle(Title);

            if (GUILayout.Button(Title, GUILayout.Width(_defaultSettingsToolControl.buttonWidth), GUILayout.Height(_defaultSettingsToolControl.buttonHeight)))
            {
                Setup();
            }
        }

        private void Setup()
        {
            foreach (var kvp in ConfigScriptableObjectControl.SettingsTypes)
            {
                CreateOrSelectSettings(kvp.Key, kvp.Value);
            }
        }

        private void CreateOrSelectSettings(string fileName, Type type)
        {
            if (!Directory.Exists(SettingsFolder))
                Directory.CreateDirectory(SettingsFolder);

            string path = $"{SettingsFolder}{fileName}.asset";
            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath(path, type);

            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorUtility.FocusProjectWindow();
                Debug.Log($"{fileName} 설정이 이미 존재합니다.");
            }
            else
            {
                ScriptableObject asset = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeObject = asset;
                EditorUtility.FocusProjectWindow();
                Debug.Log($"{fileName} ScriptableObject 가 생성되었습니다.");
            }
        }
    }
}
