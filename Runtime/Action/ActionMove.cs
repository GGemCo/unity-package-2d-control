using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    public class ActionMove
    {
        private InputManager _inputManager;
        private CharacterBase _characterBase;
        private CharacterBaseController _characterBaseController;
        public void Initialize(InputManager inputManager, CharacterBase characterBase, CharacterBaseController characterBaseController)
        {
            _inputManager = inputManager;
            _characterBase = characterBase;
            _characterBaseController = characterBaseController;
        }

        public void Move(Vector2 direction)
        {
            _characterBase.directionNormalize = direction;
            _characterBaseController.Run();
        }

        public void OnDestroy()
        {
        }
    }
}