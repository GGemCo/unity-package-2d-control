using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// Core의 캐릭터 생성 이벤트를 구독하여 ControlBase 를 자동 부착
    /// </summary>
    [DefaultExecutionOrder((int)ConfigCommonControl.ExecutionOrdering.Control)]
    public class BootstrapperAction : MonoBehaviour
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
# if GGEMCO_USE_SPINE
            
#else

#endif
            // 플레이어 타입이 아니면 return 처리 
            if (!ch.IsPlayer()) return;
            if (!ch.GetComponent<PlayerInput>())
            {
                // PlayerInput 셋팅
                var playerInput = ch.gameObject.AddComponent<PlayerInput>();
                playerInput.actions =
                    AddressableLoaderInputAction.Instance.GetInputAction(ConfigAddressableControl.InputAction.Key);
                playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            }
            
            if (!ch.GetComponent<InputManager>())
            {
                // action 처리하는 컨트롤 셋팅
                var actionController = ch.gameObject.AddComponent<InputManager>();
            }
        }

        private void OnCharacterDestroyed(CharacterBase ch)
        {
            // 필요 시 언바인드/풀 반환/로그 등 처리
        }
    }
}