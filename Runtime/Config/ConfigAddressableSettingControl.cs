using System.Collections.Generic;
using GGemCo2DCore;

namespace GGemCo2DControl
{
    public static class ConfigAddressableSettingControl
    {
        public static readonly AddressableAssetInfo AttackComboSettings = ConfigAddressableSetting.Make(nameof(AttackComboSettings));
        
        /// <summary>
        /// 로딩 씬에서 로드해야 하는 리스트
        /// </summary>
        public static readonly List<AddressableAssetInfo> NeedLoadInLoadingScene = new()
        {
            AttackComboSettings,
        };
    }
}