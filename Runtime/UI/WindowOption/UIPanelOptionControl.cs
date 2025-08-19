using System;
using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GGemCo2DControl
{
    // 프리팹의 루트에 붙는 컴포넌트
    public class UIPanelOptionControl : UIPanelOptionBase, IOptionsMenuProvider
    {
        [Header(UIWindowConstants.TitleHeaderIndividual)]
        // [SerializeField] private PlayerInput playerInput;           // 또는 InputActionAsset 직접 참조
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

        public string SectionId => "controls";
        public string DisplayName => "키 설정";
        public int Order => 50;

        // 항목 메타
        private sealed class BindingViewEntry
        {
            public UIElementOptionControlChangeKey view;
            public InputAction action;
            public int bindingIndex;         // 본인 인덱스
            public int compositeRootIndex;   // -1 = 단일 바인딩, >=0 = 소속 루트 인덱스
        }

        private readonly List<BindingViewEntry> _entries = new();
        private bool _builtAllOnce;
        private string _currentScheme; // "Keyboard&Mouse" or "Gamepad"
        
        protected override void Awake()
        {
            base.Awake();
            toggleChangePc.group = toggleGroupScheme;
            toggleChangePc.SetIsOnWithoutNotify(false);
            toggleChangePc.onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                    ChangeKeyboard();
            });
            
            toggleChangeGamePad.group = toggleGroupScheme;
            toggleChangeGamePad.SetIsOnWithoutNotify(false);
            toggleChangeGamePad.onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                    ChangeGamePad();
            });
        }
        protected override void OnDestroy()
        {
            base.OnDestroy();
            toggleChangePc?.onValueChanged.RemoveAllListeners();
            toggleChangeGamePad?.onValueChanged.RemoveAllListeners();
        }

        private void ChangeKeyboard()
        {
            if (_playerInput == null && SceneGame.Instance != null) 
            {
                _playerInput = SceneGame.Instance.player.GetComponent<PlayerInput>();
            }

            // 연결 가능한 디바이스 확인(Null 허용. 연결된 장치 자동 매칭됨)
            var kbd = Keyboard.current;
            var mouse = Mouse.current;
            // 스킴 이름과 디바이스를 함께 지정하여 강제 전환
            if (_playerInput)
            {
                _playerInput.SwitchCurrentControlScheme(ConfigCommonControl.NameControlSchemePc, kbd, mouse);
            }

            // UI 갱신 등
            SetScheme(ConfigCommonControl.NameControlSchemePc);
        }
        private void ChangeGamePad()
        {
            if (_playerInput == null && SceneGame.Instance != null) 
            {
                _playerInput = SceneGame.Instance.player.GetComponent<PlayerInput>();
            }
            var pad = Gamepad.current;
            if (pad == null)
            {
                Debug.LogWarning("연결된 Gamepad가 없습니다.");
                return;
            }

            if (_playerInput)
            {
                _playerInput.SwitchCurrentControlScheme(ConfigCommonControl.NameControlSchemeGamepad, pad);
            }

            // UI 갱신 등
            SetScheme(ConfigCommonControl.NameControlSchemeGamepad);
        }
        /// <summary>
        /// UIWindowOption에 UIPanelControl 붙이기
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="puiWindowOption"></param>
        /// <returns></returns>
        public GameObject BuildSection(Transform parent, UIWindowOption puiWindowOption)
        {
            if (puiWindowOption == null) return null;
            if (uiWindowOption == null)
            {
                uiWindowOption = puiWindowOption;
            }
            uiWindowOption.AddLayer(UIWindowOption.IndexTapButton.Control, this);
            if (!popupManager)
            {
                popupManager = uiWindowOption.popupManager;
            }
            
            gameObject.transform.SetParent(parent);
            RectTransformHelper.SetMarginZero(gameObject);
            var section = GetComponent<UIPanelOptionControl>();
            section.Init();
            if (ControlPackageManager.Instance)
            {
                ControlPackageManager.Instance.SetUIPanelControl(section);
            }

            return gameObject;
        }

        private void Init()
        {
            if (!AddressableLoaderInputAction.Instance) return;
            _asset = AddressableLoaderInputAction.Instance.GetInputAction(ConfigAddressableControl.InputAction.Key);
            if (_asset == null)
            {
                Debug.LogWarning("Input actions not assigned."); 
                return;
            }

            // 최초 1회: 모든 바인딩 항목 생성(풀)
            if (!_builtAllOnce)
            {
                BuildAllEntriesOnce();
                _builtAllOnce = true;
            }

            // 현재 스킴에 맞춰 보이기 토글
            var scheme = _playerInput ? _playerInput.currentControlScheme : ConfigCommonControl.NameControlSchemePc;
            SetScheme(scheme);
        }

        public void SetScheme(string scheme)
        {
            _currentScheme = scheme;
            ApplySchemeMask(_currentScheme);
            UpdateVisibilityForScheme(_currentScheme);
        }
        private void BuildAllEntriesOnce()
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
            view.Bind(action, binding, this);

            _items.Add(view);

            _entries.Add(new BindingViewEntry
            {
                view = view,
                action = action,
                bindingIndex = bindingIndex,
                compositeRootIndex = compositeRootIndex
            });
        }

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
        
        public void OnOpen()  { /* 필요 시 포커스/하이라이트 */ }
        public void OnClose() { /* 필요 시 정리 */ }

        // 저장/불러오기 버튼에서 호출
        protected override void OnClickConfirm()
        {
            if (_asset == null) return;
            var json = _asset.SaveBindingOverridesAsJson(); // 또는 playerInput.actions
            PlayerPrefsManager.SaveKeyBinding(json);
            SetIsChange(false);
        }

        protected override void OnClickCancel()
        {
            LoadSaveRebindingFronPlayerPrefs();
            SetIsChange(false);
        }

        protected override void OnClickReset()
        {
            PopupMetadata popupMetadata = new PopupMetadata
            {
                PopupType = PopupManager.Type.Default,
                MessageColor = Color.red,
                Title = "되돌리기", //슬롯 삭제
                Message = "디폴트 값으로 변경하시겠습니까?",
                OnConfirm = OnConfirmResetByPopup,
                ShowCancelButton = true
            };
            popupManager.ShowPopup(popupMetadata);
        }

        private void OnConfirmResetByPopup()
        {
            if (_asset == null) return;
            _asset.RemoveAllBindingOverrides();
            foreach (var i in _items) i.RefreshLabel();
            SetIsChange(false);
        }
        /// <summary>
        /// 저장하지 않고 닫을 수 있기 때문에 옵션 창이 닫힐때 현재 설정값 다시 로드
        /// </summary>
        public override bool Show(bool show)
        {
            if (show)
            {
                base.Show(true);
                LoadCurrentOptions();
                return true;
            }
            if (!isChanged)
            {
                if (!base.Show(show)) return false;
                return true;
            }
            PopupMetadata popupMetadata = new PopupMetadata
            {
                PopupType = PopupManager.Type.Default,
                MessageColor = Color.red,
                Title = "취소하기", //슬롯 삭제
                Message = "변경한 내용을 저장하지 않았습니다.\n취소하시겠습니까?",
                OnConfirm = OnConfirmByPopup,
                ShowCancelButton = true
            };
            popupManager.ShowPopup(popupMetadata);
            return false;
        }
        private void OnConfirmByPopup()
        {
            LoadCurrentOptions();
            SetButtonInteractable(false);
            // LoadCurrentOptions에서 최신으로 불러오기 때문에, 마지막에 _isChanged를 변경한다.
            SetIsChange(false);
        }

        private void LoadSaveRebindingFronPlayerPrefs()
        {
            if (_asset == null) return;
            var json = PlayerPrefsManager.LoadKeyBinding();
            _asset.LoadBindingOverridesFromJson(json);
            // UI 새로고침
            foreach (var i in _items) i.RefreshLabel();
        }
        private void LoadCurrentOptions()
        {
            LoadSaveRebindingFronPlayerPrefs();
        }

    }
}
