using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// <see cref="ActionWall"/>에서 Physics2D RaycastNonAlloc을 재사용하기 위한 보조 메서드를 포함하는 partial 구간입니다.
    /// </summary>
    /// <remarks>
    /// - GC 할당을 줄이기 위해 내부 버퍼(<see cref="_rayHits"/>)를 재사용합니다.
    /// - RaycastNonAlloc 결과는 정렬이 보장되지 않으므로, “유효한 히트 중 최단 거리”를 별도로 선택합니다.
    /// </remarks>
    public partial class ActionWall
    {
        // ============================================================
        // Physics2D NonAlloc helpers
        // ============================================================

        /// <summary>
        /// Raycast 시작점이 콜라이더 내부일 때 distance=0으로 잡히는 히트를 걸러내기 위한 임계값입니다.
        /// </summary>
        private const float RayEpsilon = 0.001f;

        /// <summary>
        /// Physics2D.RaycastNonAlloc 결과를 담는 내부 재사용 버퍼입니다.
        /// </summary>
        private readonly RaycastHit2D[] _rayHits = new RaycastHit2D[8];

        /// <summary>
        /// Physics2D.RaycastNonAlloc을 수행하고, 결과 중 "유효한 가장 가까운 히트"를 선택합니다.
        /// </summary>
        /// <param name="origin">Ray 시작점입니다.</param>
        /// <param name="dir">Ray 방향입니다. (0 벡터면 실패 처리)</param>
        /// <param name="dist">Ray 길이입니다. (0 이하이면 실패 처리)</param>
        /// <param name="layerMask">Raycast 레이어 마스크입니다.</param>
        /// <param name="nearestValidHit">
        /// 최단 거리의 유효 히트 결과입니다. (유효 히트가 없으면 default)
        /// </param>
        /// <returns>
        /// 유효 히트를 찾은 경우 RaycastNonAlloc이 반환한 히트 개수(count)를,
        /// 그렇지 않으면 0을 반환합니다.
        /// </returns>
        /// <remarks>
        /// “유효 히트”는 아래 조건을 만족하는 히트입니다.
        /// - collider가 존재한다.
        /// - <see cref="RayEpsilon"/> 이하의 거리(시작점 내부로 인한 0거리 히트)는 제외한다.
        /// </remarks>
        internal int RaycastNonAlloc(
            Vector2 origin,
            Vector2 dir,
            float dist,
            int layerMask,
            out RaycastHit2D nearestValidHit)
        {
            nearestValidHit = default;

            if (dist <= 0f || dir.sqrMagnitude < 0.0001f) return 0;

            int count = Physics2D.RaycastNonAlloc(origin, dir, _rayHits, dist, layerMask);
            if (count <= 0) return 0;

            // RaycastNonAlloc 결과는 거리 순 정렬을 보장하지 않을 수 있으므로, 최소 거리 유효 히트를 선택합니다.
            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                var h = _rayHits[i];
                if (!h.collider) continue;

                // 시작점이 콜라이더 안에 있을 때 distance=0으로 현재 벽이 히트되는 경우를 제거
                if (h.distance <= RayEpsilon) continue;

                if (h.distance < best)
                {
                    best = h.distance;
                    nearestValidHit = h;
                    found = true;
                }
            }

            return found ? count : 0;
        }

        /// <summary>
        /// 현재 캐릭터 콜라이더 바운즈 기준으로, 주어진 방향(dir) 바깥쪽으로 살짝 이동한 Raycast 시작점을 계산합니다.
        /// </summary>
        /// <param name="dir">바깥으로 밀어낼 방향입니다.</param>
        /// <param name="skin">콜라이더 바운즈 바깥으로 추가로 띄울 여유 거리입니다.</param>
        /// <returns>
        /// 콜라이더가 존재하면 바운즈 외부로 보정된 시작점을,
        /// 없으면 <see cref="Rigidbody2D.position"/>을 반환합니다.
        /// </returns>
        /// <remarks>
        /// - Ray를 콜라이더 내부에서 쏘면 0거리 히트가 발생할 수 있으므로, 시작점을 바깥으로 빼는 목적입니다.
        /// - extents와 방향 성분을 이용해 bounds 표면까지의 최소 오프셋을 근사합니다.
        /// </remarks>
        internal Vector2 GetRaycastOriginOutside(Vector2 dir, float skin = 0.02f)
        {
            var col = ColliderMapObject;
            if (col == null) return Rigidbody.position;

            var ext = col.bounds.extents;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

            // 방향 성분을 이용해 bounds 상에서 "해당 방향으로" 필요한 최소 오프셋을 계산
            float projected = Mathf.Abs(dir.x) * ext.x + Mathf.Abs(dir.y) * ext.y;
            return (Vector2)col.bounds.center + dir * (projected + Mathf.Max(skin, 0.0001f));
        }
    }
}
