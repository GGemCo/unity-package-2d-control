// using UnityEngine;
//
// namespace GGemCo2DControl
// {
//     public class ActionAttackSMB : StateMachineBehaviour
//     {
//         // 예: openAt=0.25, closeAt=0.65 → 애니메이션 25%~65% 구간에서만 다음 입력 허용
//         [Range(0,1f)] public float openAt = 0.25f;
//         [Range(0,1f)] public float closeAt = 0.65f;
//
//         public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
//         {
//             animator.GetComponent<ActionAttack>()?.OnStepEnter();
//         }
//
//         public override void OnStateUpdate(Animator animator, AnimatorStateInfo info, int layerIndex)
//         {
//             var c = animator.GetComponent<ActionAttack>();
//             if (c == null) return;
//             float t = info.normalizedTime % 1f;
//             if (t >= openAt && t <= closeAt) c.MarkCancelOpen(true);
//             else c.MarkCancelOpen(false);
//         }
//
//         public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
//         {
//             animator.GetComponent<ActionAttack>()?.OnStepExit();
//         }
//     }
// }