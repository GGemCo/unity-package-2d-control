using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// Control 패키지의 메인 메니저
    /// SceneGame 과 같은 개념
    /// </summary>
    public class ControlPackageManager : MonoBehaviour
    {
        public static ControlPackageManager Instance { get; private set; }
        private UIPanelOptionControl _uiPanelOptionControl;
        
        private void Awake()
        {
            // 게임 신 싱글톤으로 사용하기.
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            gameObject.AddComponent<BootstrapperAction>();
        }

        public void SetUIPanelControl(UIPanelOptionControl uiPanelOptionControl)
        {
            _uiPanelOptionControl = uiPanelOptionControl;
        }
        public UIPanelOptionControl GetUIPanelControl()
        {
            return _uiPanelOptionControl;
        }
    }
}