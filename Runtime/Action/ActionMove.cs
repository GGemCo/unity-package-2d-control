using UnityEngine;

namespace GGemCo2DControl
{
    public class ActionMove : ActionBase
    {
        private bool _canMoveVertical;
        
        protected override void ApplySettings()
        {
            _canMoveVertical = playerActionSettings.canMoveVertical;
        }

        public void Move(Vector2 direction)
        {
            if (!_canMoveVertical)
            {
                if (Mathf.Approximately(direction.y, 1) || Mathf.Approximately(direction.y, -1))
                {
                    actionCharacterBase.Stop();
                    return;
                }
                direction.y = 0;
            }
            
            actionCharacterBase.directionNormalize = direction.normalized;
            actionCharacterBase.SetStatusRun();    
            actionCharacterBaseController.Run();
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
            actionCharacterBase.directionNormalize = direction.normalized;
            actionCharacterBaseController.Run();
        }
    }
}