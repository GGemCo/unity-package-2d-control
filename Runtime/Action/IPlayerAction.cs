using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 플레이어 액션 공통 수명주기 계약
    /// </summary>
    public interface IPlayerAction
    {
        /// <param name="owner">액션이 부착된(혹은 관리되는) MonoBehaviour</param>
        /// <param name="character">캐릭터 베이스</param>
        /// <param name="controller">캐릭터 컨트롤러</param>
        void Initialize(MonoBehaviour owner, CharacterBase character, CharacterBaseController controller);
        void Update();      // 필요 없으면 no-op 구현 허용
        void OnDestroy();
    }
}