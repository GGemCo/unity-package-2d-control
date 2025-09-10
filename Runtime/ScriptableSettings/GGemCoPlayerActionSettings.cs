using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 플레이어 action 설정
    /// </summary>
    [CreateAssetMenu(fileName = ConfigScriptableObjectControl.PlayerAction.FileName, menuName = ConfigScriptableObjectControl.PlayerAction.MenuName, order = ConfigScriptableObjectControl.PlayerAction.Ordering)]
    public class GGemCoPlayerActionSettings : ScriptableObject
    {
        [Header("이동 설정")] 
        [Tooltip("위, 아래 이동 가능 여부")]
        public bool canMoveVertical;
        
        [Header("점프 설정")]
        [Tooltip("점프 최고 높이 (유닛 단위)")]
        public float jumpHeight;
        [Tooltip("점프 속도. 지면 → 정점까지 걸리는 시간 (초)")]
        public float jumpSpeed;
        
        [Header("대시 설정")]
        [Tooltip("대시 거리 (유닛 단위)")]
        public float dashDistance;
        [Tooltip("대시 속도. (초)")]
        public float dashDuration;
        [Tooltip("대시 Easing")]
        public Easing.EaseType dashEasing;
        [Tooltip("대시 중 공격 가능 여부")]
        public bool canAttackPlayDashing;
        // [Tooltip("대시 중 이동 가능 여부")]
        // true 일 경우 방향키를 누른 상태로 대시를 사용하면 대시를 하지 못 한다.
        // public bool canMovePlayDashing;
        [Tooltip("대시 중 점프 가능 여부")]
        public bool canJumpPlayDashing;
        [Tooltip("점프 중 대시 가능 여부")]
        public bool canDashPlayJumping;
        
        [Header("등반 설정")]
        [Tooltip("디폴트 등반 올라가기 속도.\n플레이어 Move Speed 기준으로 계산\n1 = 100%, 0.5 = 50%\n각 사다리별로 다르게 설정 가능함")]
        public float climbSpeed;
        [Tooltip("점프 중 등반 가능 여부")]
        public bool canClimbingPlayJumping;
        [Tooltip("등반 중 점프 가능 여부")]
        public bool canJumpPlayClimbing;
        [Tooltip("등반 중 대시 가능 여부")]
        public bool canDashPlayClimbing;
        [Tooltip("등반 중 좌/우 이동 가능 여부")]
        public bool canMoveSidePlayClimbing;

        [Header("밀기/당기기 설정")]
        public float pushMoveSpeed;
    }
}