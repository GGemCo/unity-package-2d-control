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

        /// <summary>
        /// 캐릭터 생성/파괴 이벤트를 구독하여 플레이어 입력 컴포넌트 부착 시점을 감지합니다.
        /// </summary>
        private void OnEnable()
        {
            CharacterManager.OnCharacterSpawned += OnCharacterSpawned;
            CharacterManager.OnCharacterDestroyed += OnCharacterDestroyed;
        }

        /// <summary>
        /// 부트스트랩 이벤트 구독을 해제합니다.
        /// </summary>
        private void OnDisable()
        {
            CharacterManager.OnCharacterSpawned -= OnCharacterSpawned;
            CharacterManager.OnCharacterDestroyed -= OnCharacterDestroyed;
        }

        /// <summary>
        /// 플레이어 캐릭터가 생성되면 PlayerInput, InputManager, 모바일 HUD 부트스트랩을 연결합니다.
        /// 입력 매니저는 생성 직후 명시적으로 Initialize/Activate하여 PlayerInput 바인딩 시점을 제어합니다.
        /// </summary>
        /// <param name="ch">생성된 캐릭터입니다.</param>
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

            InputManager inputManager = ch.GetComponent<InputManager>();
            if (inputManager == null)
            {
                inputManager = ch.gameObject.AddComponent<InputManager>();
            }

            ActivateInputManager(inputManager);

            MobileInputHudService.EnsureInstance();

            MobileInputHudBootstrap hudBootstrap = ch.GetComponent<MobileInputHudBootstrap>();
            if (hudBootstrap == null)
            {
                ch.gameObject.AddComponent<MobileInputHudBootstrap>();
            }
            else if (hudBootstrap.isActiveAndEnabled)
            {
                hudBootstrap.BindNow();
            }
        }


        /// <summary>
        /// 플레이어 입력 매니저를 명시적 초기화/활성화 단계로 전환합니다.
        /// PlayerInput 컴포넌트가 준비된 뒤 호출하여, 캐릭터 생성 중 입력 콜백이 먼저 바인딩되는 상황을 방지합니다.
        /// </summary>
        /// <param name="inputManager">초기화할 플레이어 입력 매니저입니다.</param>
        private static void ActivateInputManager(InputManager inputManager)
        {
            if (inputManager == null)
            {
                return;
            }

            inputManager.Initialize(null);
            inputManager.Activate(null);
        }

        /// <summary>
        /// 플레이어 캐릭터가 제거될 때 입력 매니저와 모바일 HUD 연결을 해제합니다.
        /// </summary>
        /// <param name="ch">제거되는 캐릭터입니다.</param>
        private void OnCharacterDestroyed(CharacterBase ch)
        {
            if (ch == null || !ch.IsPlayer())
            {
                return;
            }

            InputManager inputManager = ch.GetComponent<InputManager>();
            inputManager?.Deinitialize();

            MobileInputHudService.Instance?.UnbindPlayer(ch.GetComponent<PlayerInput>());
        }
    }
}
