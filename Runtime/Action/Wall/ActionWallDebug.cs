#if UNITY_EDITOR
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// <see cref="ActionWall"/>의 디버그 시각화(Gizmo) 관련 기능을 정의하는 partial 구간입니다.
    /// </summary>
    /// <remarks>
    /// - UNITY_EDITOR 전용 코드로, 런타임 빌드에는 포함되지 않습니다.
    /// - 벽 점프 예측/이동 경로를 Ray 형태로 시각화하여 튜닝과 디버깅을 돕습니다.
    /// </remarks>
    public partial class ActionWall
    {
        // ============================================================
        // Debug (Gizmo)
        // ============================================================

        /// <summary>
        /// Gizmo로 그릴 디버그 Ray 정보를 담는 경량 구조체입니다.
        /// </summary>
        /// <remarks>
        /// Raycast 결과(히트 여부/지점)와 시각화 색상을 함께 보관합니다.
        /// </remarks>
        public struct DebugRay
        {
            /// <summary>유효한 디버그 Ray 데이터인지 여부입니다.</summary>
            public bool IsValid;

            /// <summary>Ray 시작점입니다.</summary>
            public Vector2 Origin;

            /// <summary>Ray 방향(정규화됨)입니다.</summary>
            public Vector2 Direction;

            /// <summary>Ray 길이입니다.</summary>
            public float Distance;

            /// <summary>Raycast 히트 여부입니다.</summary>
            public bool IsHit;

            /// <summary>Raycast 히트 지점입니다.</summary>
            public Vector2 HitPoint;

            /// <summary>Gizmo로 그릴 색상입니다.</summary>
            public Color Color;

            /// <summary>
            /// 디버그 Ray 데이터를 초기 상태로 리셋합니다.
            /// </summary>
            public void Clear()
            {
                IsValid = false;
                Origin = Vector2.zero;
                Direction = Vector2.right;
                Distance = 0f;
                IsHit = false;
                HitPoint = Vector2.zero;
                Color = Color.white;
            }

            /// <summary>
            /// 디버그 Ray 데이터를 설정합니다.
            /// </summary>
            /// <param name="o">Ray 시작점입니다.</param>
            /// <param name="d">Ray 방향입니다. 길이가 매우 작으면 +X 방향으로 보정됩니다.</param>
            /// <param name="dist">Ray 길이입니다. 음수는 0으로 보정됩니다.</param>
            /// <param name="hit">Raycast 히트 여부입니다.</param>
            /// <param name="hitPt">Raycast 히트 지점입니다.</param>
            /// <param name="c">Gizmo로 그릴 색상입니다.</param>
            public void Set(Vector2 o, Vector2 d, float dist, bool hit, Vector2 hitPt, Color c)
            {
                IsValid = true;
                Origin = o;
                Direction = d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right;
                Distance = Mathf.Max(0f, dist);
                IsHit = hit;
                HitPoint = hitPt;
                Color = c;
            }
        }

        /// <summary>
        /// 벽 점프 "예측 경로"를 시각화하기 위한 디버그 Ray입니다.
        /// </summary>
        public DebugRay DebugJumpPredictRay;

        /// <summary>
        /// 벽 점프 실제 "이동/탐색 경로"를 시각화하기 위한 디버그 Ray입니다.
        /// </summary>
        public DebugRay DebugJumpTravelRay;

        /// <summary>
        /// 벽 점프 예측 Ray 정보를 설정합니다. (청록색)
        /// </summary>
        internal void DebugSetJumpPredictRay(
            Vector2 origin,
            Vector2 dir,
            float dist,
            bool hit,
            Vector2 hitPoint)
        {
            DebugJumpPredictRay.Set(
                origin,
                dir,
                dist,
                hit,
                hitPoint,
                new Color(0f, 1f, 1f, 1f));
        }

        /// <summary>
        /// 벽 점프 이동/탐색 Ray 정보를 설정합니다. (노란색)
        /// </summary>
        internal void DebugSetJumpTravelRay(
            Vector2 origin,
            Vector2 dir,
            float dist,
            bool hit,
            Vector2 hitPoint)
        {
            DebugJumpTravelRay.Set(
                origin,
                dir,
                dist,
                hit,
                hitPoint,
                new Color(1f, 1f, 0f, 1f));
        }
    }
}
#endif
