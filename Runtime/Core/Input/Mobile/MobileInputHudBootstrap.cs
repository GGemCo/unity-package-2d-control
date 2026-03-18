using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// 플레이어에 부착되어 MobileInputHudService와 현재 PlayerInput을 연결합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileInputHudBootstrap : MonoBehaviour
    {
        private PlayerInput _playerInput;
        private MobileInputHudService _service;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
        }

        private void OnEnable()
        {
            BindNow();
        }

        public void BindNow()
        {
            if (_playerInput == null)
            {
                _playerInput = GetComponent<PlayerInput>();
            }

            _service = MobileInputHudService.EnsureInstance();
            _service?.BindPlayer(_playerInput);
        }

        private void OnDisable()
        {
            _service?.UnbindPlayer(_playerInput);
        }
    }
}
