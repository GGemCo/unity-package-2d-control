using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    public class ActionMove
    {
        private InputManager _inputManager;
        private CharacterBase _characterBase;
        private CharacterBaseController _characterBaseController;
        private bool _canMoveVertical;
        public void Initialize(InputManager inputManager, CharacterBase characterBase, CharacterBaseController characterBaseController)
        {
            _inputManager = inputManager;
            _characterBase = characterBase;
            _characterBaseController = characterBaseController;
            _canMoveVertical = AddressableLoaderSettingsControl.Instance.playerActionSettings.canMoveVertical;
        }

        public void Move(Vector2 direction)
        {
            if (!_canMoveVertical)
            {
                if (Mathf.Approximately(direction.y, 1) || Mathf.Approximately(direction.y, -1))
                {
                    _characterBase.Stop();
                    return;
                }
                direction.y = 0;
            }
            
            _characterBase.directionNormalize = direction.normalized;
            _characterBase.SetStatusRun();    
            _characterBaseController.Run();
        }

        public void OnDestroy()
        {
        }
        /// <summary>
        /// 점프 중 이동하기
        /// </summary>
        /// <param name="direction"></param>
        public void JumpMove(Vector2 direction)
        {
            if (!_canMoveVertical)
            {
                if (Mathf.Approximately(direction.y, 1) || Mathf.Approximately(direction.y, -1))
                {
                    return;
                }
                direction.y = 0;
            }
            _characterBase.directionNormalize = direction.normalized;
            _characterBaseController.Run();
        }
    }
}