using GGemCo2DCore;

namespace GGemCo2DCoreEditor
{
    public class DefaultSceneEditorControl : DefaultSceneEditor
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            packageType = ConfigPackageInfo.PackageType.Control;
        }
    }
}