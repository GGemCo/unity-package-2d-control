using UnityEngine;

namespace GGemCo2DControl
{
    /// 플레이어가 상호작용할 수 있는 대상에 부착
    public interface IInteraction
    {
        /// 상호작용 우선순위(값이 작을수록 우선). ex) 사다리 > 박스 등
        int Priority { get; }

        /// 현재 이 오브젝트가 상호작용 가능 상태인지
        bool IsAvailable(GameObject interactor);

        /// UI 등에 표시할 힌트(예: "F: 오르기 / 내리기", "F: 밀기/당기기")
        string GetHint();

        /// 상호작용 시작(성공 시 true)
        bool BeginInteract(GameObject interactor);

        /// 상호작용 종료
        void EndInteract(GameObject interactor);
    }
}