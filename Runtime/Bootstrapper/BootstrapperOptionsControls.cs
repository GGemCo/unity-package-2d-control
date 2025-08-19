using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    public sealed class BootstrapperOptionsControls : MonoBehaviour
    {
        [SerializeField] private UIPanelOptionControl panelOptionControl;

        private void Awake()
        {
            var reg = FindFirstObjectByType<UIWindowOptionsExtensionRegistry>();
            if (reg && panelOptionControl)
            {
                // 프리팹의 인스턴스(자식 생성은 provider 내부 BuildSection에서 처리)
                var inst = Instantiate(panelOptionControl);
                reg.Register(inst);
            }
            else
            {
                Debug.LogWarning("OptionsExtensionRegistry or panelOptionControl not found. Controls tab skipped.");
            }
        }
    }
}