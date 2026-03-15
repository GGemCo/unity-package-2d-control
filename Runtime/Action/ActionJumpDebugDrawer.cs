#if UNITY_EDITOR
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// <see cref="ActionJump"/>의 접지/천장 판정 박스를 SceneView Gizmo로 시각화하는 프록시입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActionJumpDebugDrawer : MonoBehaviour
    {
        private static readonly Color GroundHitColor = new(0.15f, 0.95f, 0.25f, 1f);
        private static readonly Color GroundMissColor = new(0.95f, 0.25f, 0.25f, 1f);
        private static readonly Color CeilingHitColor = new(1f, 0.85f, 0.1f, 1f);
        private static readonly Color CeilingMissColor = new(0.45f, 0.65f, 1f, 1f);

        private ActionJump _jump;
        private GGemCoPlayerActionSettings _settings;

        /// <summary>
        /// 디버그 드로어가 참조할 점프 액션과 설정을 바인딩합니다.
        /// </summary>
        public void Bind(ActionJump jump, GGemCoPlayerActionSettings settings)
        {
            _jump = jump;
            _settings = settings;
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            if (_jump == null || _settings == null) return;
            if (!_settings.EnableJumpProbeDebugGizmos) return;

            if (_jump.TryGetGroundProbeBounds(out var groundCenter, out var groundSize, out var isGrounded))
            {
                DrawProbe(groundCenter, groundSize, isGrounded ? GroundHitColor : GroundMissColor);
            }

            if (_jump.TryGetCeilingProbeBounds(out var ceilingCenter, out var ceilingSize, out var isCeilingHit))
            {
                DrawProbe(ceilingCenter, ceilingSize, isCeilingHit ? CeilingHitColor : CeilingMissColor);
            }
        }

        private static void DrawProbe(Vector2 center, Vector2 size, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(center, size);
            Gizmos.DrawSphere(center, 0.02f);
        }
    }
}
#endif
