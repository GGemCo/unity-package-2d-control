using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// Core의 캐릭터 생성 이벤트를 구독하여 ControlBase 를 자동 부착
    /// </summary>
    [DefaultExecutionOrder((int)ConfigCommon.ExecutionOrdering.Control)]
    public class BootstrapperControl : MonoBehaviour
    {
        [SerializeField] private bool addIfMissing = true;

        private void OnEnable()
        {
            CharacterManager.OnCharacterSpawned   += OnCharacterSpawned;
            CharacterManager.OnCharacterDestroyed += OnCharacterDestroyed;
        }

        private void OnDisable()
        {
            CharacterManager.OnCharacterSpawned   -= OnCharacterSpawned;
            CharacterManager.OnCharacterDestroyed -= OnCharacterDestroyed;
        }

        private void OnCharacterSpawned(CharacterBase ch)
        {
            if (!addIfMissing) return;
            if (ch.GetComponent<ControlBase>()) return;

            var controller = ch.gameObject.AddComponent<ControlBase>();
            // 필요 시 브릿지/그래프 주입
            // controller.SetGraph(defaultGraph);
            // controller.AutoBindBridgesIfPossible(ch.gameObject); // 구현 방식은 패키지 내부 정책대로
        }

        private void OnCharacterDestroyed(CharacterBase ch)
        {
            // 필요 시 언바인드/풀 반환/로그 등 처리
        }
    }
}