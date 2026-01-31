#if UNITY_EDITOR
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// <see cref="ActionWall"/> 디버그 정보를 SceneView에 Gizmo로 시각화하는 프록시(MonoBehaviour)입니다.
    /// </summary>
    /// <remarks>
    /// <see cref="ActionWall"/>은 MonoBehaviour가 아니므로 <c>OnDrawGizmos*</c> 콜백을 직접 받을 수 없습니다.
    /// 이 컴포넌트는 런타임 중 ActionWall이 기록한 디버그 Ray 정보를 읽어 선택 상태에서만 표시합니다.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ActionWallDebugDrawer : MonoBehaviour
    {
        private ActionWall _wall;
        private GGemCoPlayerActionSettings _settings;

        /// <summary>
        /// 디버그 드로어가 참조할 <see cref="ActionWall"/>과 설정을 바인딩합니다.
        /// </summary>
        /// <param name="wall">디버그 정보를 제공하는 벽 액션 인스턴스입니다.</param>
        /// <param name="settings">디버그 표시 여부 등 사용자 설정입니다.</param>
        public void Bind(ActionWall wall, GGemCoPlayerActionSettings settings)
        {
            _wall = wall;
            _settings = settings;
        }

        /// <summary>
        /// 오브젝트가 선택된 상태에서만 Gizmo를 그립니다.
        /// </summary>
        /// <remarks>
        /// - 플레이 중이며, 설정에서 디버그 Gizmo가 활성화된 경우에만 그립니다.
        /// - 예측 Ray/이동 Ray를 각각 다른 색으로 표시합니다.
        /// </remarks>
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            if (_wall == null || _settings == null) return;
            if (!_settings.enableWallDebugGizmos) return;

            DrawRay(_wall.DebugJumpPredictRay);
            DrawRay(_wall.DebugJumpTravelRay);
        }

        /// <summary>
        /// <see cref="ActionWall.DebugRay"/> 정보를 기반으로 Gizmo 라인/히트 포인트를 그립니다.
        /// </summary>
        /// <param name="ray">그릴 디버그 Ray 데이터입니다.</param>
        private static void DrawRay(in ActionWall.DebugRay ray)
        {
            if (!ray.IsValid) return;

            Gizmos.color = ray.Color;
            Gizmos.DrawLine(ray.Origin, ray.Origin + ray.Direction * ray.Distance);

            if (ray.IsHit)
            {
                Gizmos.DrawSphere(ray.HitPoint, 0.05f);
            }
        }
    }
}
#endif
