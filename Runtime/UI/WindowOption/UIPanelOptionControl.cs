using System;
using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GGemCo2DControl
{
    /// <summary>
    /// 조작 키 변경하기 패널
    /// </summary>
    public class UIPanelOptionControl : UIPanelOptionBase
    {
        [Header(UIWindowConstants.TitleHeaderIndividual)]
        [Tooltip("키 Element가 들어갈 Panel")]
        [SerializeField] private Transform listParent;              // 항목이 그려질 부모(스크롤 콘텐츠)
        [Tooltip("키 Element 프리팹")]
        [SerializeField] private UIElementOptionControlChangeKey uiElementOptionControlChangeKeyPrefab;     // 한 항목의 UI 프리팹
        [Tooltip("스킴 토글 그룹")]
        [SerializeField] private ToggleGroup toggleGroupScheme;
        [Tooltip("스킴 PC")]
        [SerializeField] private Toggle toggleChangePc;
        [Tooltip("스킴 게임패드")]
        [SerializeField] private Toggle toggleChangeGamePad;

        private InputActionAsset _asset;
        private readonly List<UIElementOptionControlChangeKey> _items = new();
        private PlayerInput _playerInput;
#if UNITY_EDITOR
        private void OnValidate()
        {
            UIAssertionsChecker.Require(this, listParent, nameof(listParent));
            UIAssertionsChecker.Require(this, uiElementOptionControlChangeKeyPrefab, nameof(uiElementOptionControlChangeKeyPrefab));
            UIAssertionsChecker.Require(this, toggleGroupScheme, nameof(toggleGroupScheme));
            UIAssertionsChecker.Require(this, toggleChangePc, nameof(toggleChangePc));
            UIAssertionsChecker.Require(this, toggleChangeGamePad, nameof(toggleChangeGamePad));
        }
#endif
        // 항목 메타
        private sealed class BindingViewEntry
        {
            public UIElementOptionControlChangeKey view;
            public InputAction action;
            public int bindingIndex;         // 본인 인덱스
            public int compositeRootIndex;   // -1 = 단일 바인딩, >=0 = 소속 루트 인덱스
        }

        private readonly List<BindingViewEntry> _entries = new();
        private bool _createAllOnce;
        private string _currentScheme; // "Keyboard&Mouse" or "Gamepad"
        
        protected override void Awake()
        {
            if (!AddressableLoaderInputAction.Instance)
            {
                enabled = false;
                return;
            }
            base.Awake();
            
            // 토글 리스너 등록
            if (toggleChangePc)
                toggleChangePc.onValueChanged.AddListener(isOn =>
                    OnSchemeToggleChanged(ConfigCommonControl.NameControlSchemePc, isOn));

            if (toggleChangeGamePad)
                toggleChangeGamePad.onValueChanged.AddListener(isOn =>
                    OnSchemeToggleChanged(ConfigCommonControl.NameControlSchemeGamepad, isOn));

            toggleChangePc.group = toggleGroupScheme;
            toggleChangeGamePad.group = toggleGroupScheme;
            Initialize();
        }
        protected override void OnDestroy()
        {
            base.OnDestroy();
            toggleChangePc?.onValueChanged.RemoveAllListeners();
            toggleChangeGamePad?.onValueChanged.RemoveAllListeners();
        }
        /// <summary>
        /// 초기 셋팅
        /// 각각의 키 Element 만들기
        /// 현재 적용중인 Scheme 셋팅하기
        /// </summary>
        private void Initialize()
        {
            if (!AddressableLoaderInputAction.Instance)
            {
                GcLogger.LogError($"{nameof(AddressableLoaderInputAction)} 싱글톤이 생성되지 않았습니다.");
                return;
            }
            _asset = AddressableLoaderInputAction.Instance.GetInputAction(ConfigAddressableControl.InputAction.Key);
            if (_asset == null)
            {
                Debug.LogWarning("Input actions not assigned."); 
                return;
            }

            // 최초 1회: 모든 바인딩 항목 생성(풀)
            if (!_createAllOnce)
            {
                CreateAllElementOnce();
                _createAllOnce = true;
            }

            // 현재 스킴에 맞춰 보이기 토글
            var scheme = _playerInput ? _playerInput.currentControlScheme : ConfigCommonControl.NameControlSchemePc;
            if (scheme == ConfigCommonControl.NameControlSchemePc)
            {
                toggleChangePc.SetIsOnWithoutNotify(true);
                toggleChangeGamePad.SetIsOnWithoutNotify(false);
            }
            else if (scheme == ConfigCommonControl.NameControlSchemeGamepad)
            {
                toggleChangePc.SetIsOnWithoutNotify(false);
                toggleChangeGamePad.SetIsOnWithoutNotify(true);
            }
            SetScheme(scheme);
        }
        /// <summary>
        /// 현재 적용중인 Scheme 셋팅
        /// </summary>
        /// <param name="scheme"></param>
        private void SetScheme(string scheme)
        {
            _currentScheme = scheme;
            ApplySchemeMask(_currentScheme);
            UpdateVisibilityForScheme(_currentScheme);
        }
        /// <summary>
        /// 현재 적용중인 Scheme 가져오기
        /// </summary>
        /// <returns></returns>
        public string GetScheme()
        {
            return _currentScheme;
        }
        /// <summary>
        /// 키 Element 만들기
        /// </summary>
        private void CreateAllElementOnce()
        {
            _entries.Clear();
            _items.Clear();

            foreach (var map in _asset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    var bindings = action.bindings;
                    for (int i = 0; i < bindings.Count; i++)
                    {
                        var b = bindings[i];

                        if (b.isComposite)
                        {
                            // 루트는 UI 슬롯을 만들지 않고, 파트들만 생성(원하면 대표 슬롯 1개를 만들도록 변경 가능)
                            int root = i;
                            int part = i + 1;
                            while (part < bindings.Count && bindings[part].isPartOfComposite)
                            {
                                CreateElement(action, part, compositeRootIndex: root);
                                part++;
                            }
                            i = part - 1; // for 인덱스 점프
                        }
                        else if (b.isPartOfComposite)
                        {
                            // 위에서 composite 루트에서 처리하므로 스킵
                            continue;
                        }
                        else
                        {
                            // 단일 바인딩
                            CreateElement(action, i, compositeRootIndex: -1);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 키 element 만들기
        /// </summary>
        /// <param name="action"></param>
        /// <param name="bindingIndex"></param>
        /// <param name="compositeRootIndex"></param>
        private void CreateElement(InputAction action, int bindingIndex, int compositeRootIndex)
        {
            var view = Instantiate(uiElementOptionControlChangeKeyPrefab, listParent);
            // 초기에는 전부 만들어 두고, 이후 스킴에 따라 Active만 토글
            var binding = action.bindings[bindingIndex];
            view.Bind(action, binding);

            _items.Add(view);

            _entries.Add(new BindingViewEntry
            {
                view = view,
                action = action,
                bindingIndex = bindingIndex,
                compositeRootIndex = compositeRootIndex
            });
        }
        /// <summary>
        /// UIWindowOption 셋팅하기
        /// Element에서 UIWindowOption을 사용하기 위해, UIWindowOption 셋팅 후 Element에도 UIWindowOption을 넣어준다.
        /// </summary>
        /// <param name="puiWindowOption"></param>
        public override void SetWindowOption(UIWindowOption puiWindowOption)
        {
            base.SetWindowOption(puiWindowOption);
            foreach (var uiElementOptionControlChangeKey in _items)
            {
                uiElementOptionControlChangeKey.SetUIWindowOption(puiWindowOption, this);
            }
        }
        /// <summary>
        /// 현재 선택된 Scheme 에 맞는 키 Element만 보여주기
        /// </summary>
        /// <param name="schemeName"></param>
        private void UpdateVisibilityForScheme(string schemeName)
        {
            foreach (var e in _entries)
            {
                bool visible = BindingInScheme(e, schemeName);
                if (e.view.gameObject.activeSelf != visible)
                    e.view.gameObject.SetActive(visible);

                // 스킴 변경 후 레이블 갱신(오버라이드/마스크 영향 반영)
                if (visible) e.view.RefreshLabel();
            }
        }

        // binding.groups(세미콜론 구분)에 스킴명이 포함되는지 검사
        // 파트의 groups가 비어 있을 수 있으므로 composite 루트를 fallback으로 검사
        private bool BindingInScheme(BindingViewEntry entry, string schemeName)
        {
            if (string.IsNullOrEmpty(schemeName)) return true;

            var bindings = entry.action.bindings;
            var current = bindings[entry.bindingIndex];

            if (GroupsContain(current.groups, schemeName)) return true;
            if (entry.compositeRootIndex < 0) return false;
            var root = bindings[entry.compositeRootIndex];
            return GroupsContain(root.groups, schemeName);
        }

        private static bool GroupsContain(string groups, string schemeName)
        {
            if (string.IsNullOrEmpty(groups)) return false;
            var arr = groups.Split(';');
            foreach (var t in arr)
            {
                if (string.Equals(t.Trim(), schemeName, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private void ApplySchemeMask(string schemeName)
        {
            if (_asset == null) return;
            _asset.bindingMask = InputBinding.MaskByGroup(schemeName);
        }
        /// <summary>
        /// 현재 저장되어있는 값으로 다시 셋팅하기
        /// </summary>
        protected override void RefreshFromModel()
        {
            if (_asset == null) return;
            var json = PlayerPrefsManager.LoadKeyBinding();
            _asset.LoadBindingOverridesFromJson(json);

            RefreshAllLabels();
        }
        /// <summary>
        /// 디폴트로 리셋
        /// </summary>
        protected override void ResetToDefault()
        {
            if (_asset == null) return;
            _asset.RemoveAllBindingOverrides();

            RefreshAllLabels();
            MarkDirty(true);
        }
        /// <summary>
        /// 옵션 설정 저장하기
        /// </summary>
        public override bool TryApply()
        {
            if (_asset == null) return false;
            var json = _asset.SaveBindingOverridesAsJson(); // 또는 playerInput.actions
            PlayerPrefsManager.SaveKeyBinding(json);
            return true;
        }
        /// <summary>
        /// 변경한 것이 있을때, 취소하기
        /// </summary>
        public override void Revert()
        {
            RefreshFromModel();
        }
        /// <summary>
        /// Element에 각 key 텍스트 업데이트
        /// </summary>
        public void RefreshAllLabels()
        {
            foreach (var i in _items) i.RefreshLabel();
        }
        /// <summary>
        /// 토글 클릭 시 호출되는 스킴 전환 처리
        /// </summary>
        private void OnSchemeToggleChanged(string scheme, bool isOn)
        {
            if (!isOn) return;                  // Off 이벤트는 무시

            if (scheme == ConfigCommonControl.NameControlSchemePc)
            {
                ChangePc();
            }
            else if (scheme == ConfigCommonControl.NameControlSchemeGamepad)
            {
                ChangeGamePad();
            }
        }
        /// <summary>
        /// PC Scheme으로 변경하기
        /// </summary>
        private void ChangePc()
        {
            if (_playerInput == null && SceneGame.Instance != null) 
            {
                _playerInput = SceneGame.Instance.player.GetComponent<PlayerInput>();
            }

            // 스킴 이름과 디바이스를 함께 지정하여 강제 전환
            if (_playerInput)
            {
                // 연결 가능한 디바이스 확인(Null 허용. 연결된 장치 자동 매칭됨)
                var kbd = Keyboard.current;
                var mouse = Mouse.current;
                _playerInput.SwitchCurrentControlScheme(ConfigCommonControl.NameControlSchemePc, kbd, mouse);
            }

            // UI 갱신 등
            SetScheme(ConfigCommonControl.NameControlSchemePc);
        }
        /// <summary>
        /// 게임 패드 scheme으로 변경하기
        /// </summary>
        private void ChangeGamePad()
        {
            if (_playerInput == null && SceneGame.Instance != null) 
            {
                _playerInput = SceneGame.Instance.player.GetComponent<PlayerInput>();
            }

            if (_playerInput)
            {
                var pad = Gamepad.current;
                if (pad == null)
                {
                    Debug.LogWarning("연결된 Gamepad가 없습니다.");
                    return;
                }
                _playerInput.SwitchCurrentControlScheme(ConfigCommonControl.NameControlSchemeGamepad, pad);
            }

            // UI 갱신 등
            SetScheme(ConfigCommonControl.NameControlSchemeGamepad);
        }
    }
}
