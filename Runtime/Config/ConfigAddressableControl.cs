using GGemCo2DCore;

namespace GGemCo2DControl
{
    public class ConfigAddressableControl
    {
        // Input Action
        public static readonly AddressableAssetInfo InputAction = new(
            $"{ConfigDefine.NameSDK}_InputAction",
            $"{GGemCo2DCore.ConfigAddressables.Path}/InputAction/ControlsMain.inputactions",
            $"{ConfigDefine.NameSDK}_InputAction"
        );

    }
}