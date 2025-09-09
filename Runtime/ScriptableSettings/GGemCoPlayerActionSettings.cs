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
        [Tooltip("대시 중 이동 가능 여부")]
        public bool canMovePlayDashing;
        [Tooltip("대시 중 점프 가능 여부")]
        public bool canJumpPlayDashing;
        [Tooltip("점프 중 대시 가능 여부")]
        public bool canDashPlayJumping;
    }
}