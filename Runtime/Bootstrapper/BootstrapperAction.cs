using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// Core의 캐릭터 생성 이벤트를 구독하여 Control 관련 컴포넌트를 자동 부착합니다.
    /// </summary>
    public class BootstrapperAction : MonoBehaviour
    {
        [SerializeField] private bool addIfMissing = true;

        private void OnEnable()
        {
            CharacterManager.OnCharacterSpawned += OnCharacterSpawned;
            CharacterManager.OnCharacterDestroyed += OnCharacterDestroyed;
        }

        private void OnDisable()
        {
            CharacterManager.OnCharacterSpawned -= OnCharacterSpawned;
            CharacterManager.OnCharacterDestroyed -= OnCharacterDestroyed;
        }

        private void OnCharacterSpawned(CharacterBase ch)
        {
            if (!addIfMissing || ch == null)
            {
                return;
            }

            if (!ch.IsPlayer())
            {
                return;
            }

            PlayerInput playerInput = ch.GetComponent<PlayerInput>();
            if (playerInput == null)
            {
                playerInput = ch.gameObject.AddComponent<PlayerInput>();
                playerInput.actions = AddressableLoaderInputAction.Instance.GetInputAction(ConfigAddressableControl.InputAction.Key);
                playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            }

            if (ch.GetComponent<InputManager>() == null)
            {
                ch.gameObject.AddComponent<InputManager>();
            }

            MobileInputHudService service = MobileInputHudService.EnsureInstance();
            if (service != null)
            {
                service.BindPlayer(playerInput);
            }

            if (ch.GetComponent<MobileInputHudBootstrap>() == null)
            {
                ch.gameObject.AddComponent<MobileInputHudBootstrap>();
            }
        }

        private void OnCharacterDestroyed(CharacterBase ch)
        {
            if (ch == null || !ch.IsPlayer())
            {
                return;
            }

            MobileInputHudService.Instance?.UnbindPlayer(ch.GetComponent<PlayerInput>());
        }
    }
}
