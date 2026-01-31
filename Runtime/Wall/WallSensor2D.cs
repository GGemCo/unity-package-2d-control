using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 캐릭터의 측면 벽 접촉을 표준화하여 제공하는 센서.
    /// - colliderMapObject(CapsuleCollider2D)를 기반으로 BoxCast(또는 CapsuleCast)로 측면을 검사한다.
    /// - ActionWallHang/Slide/Jump가 이 센서만 의존하도록 하여, 물리 판정 변경 시 한 곳만 수정하도록 한다.
    /// </summary>
    public sealed class WallSensor2D
    {
        public struct WallHit
        {
            public bool IsHit;
            public Vector2 Normal;   // 벽 법선(좌/우 판정)
            public Vector2 Point;    // 히트 포인트(월드)
            public Collider2D Collider;
        }

        private readonly CapsuleCollider2D _col;
        private readonly Rigidbody2D _rb;

        private LayerMask _wallMask;
        private float _checkDistance;

        // 캐시(할당 최소화)
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[4];

        public WallSensor2D(CapsuleCollider2D col, Rigidbody2D rb)
        {
            _col = col;
            _rb = rb;
        }

        public void Configure(LayerMask wallMask, float checkDistance)
        {
            _wallMask = wallMask;
            _checkDistance = Mathf.Max(0.01f, checkDistance);
        }

        /// <summary>
        /// 지정한 방향으로 벽을 검사한다.
        /// directionX: -1(왼쪽), +1(오른쪽)
        /// </summary>
        public WallHit CheckSide(int directionX, bool ignoreSelfHit = false)
        {
            if (_col == null) return default;

            // CapsuleCollider2D.bounds 기반으로 BoxCast(가장 안정적/가벼움)
            var bounds = _col.bounds;

            // 두께는 살짝 줄여서 '코너'에서 과검출을 줄임
            var size = new Vector2(bounds.size.x * 0.92f, bounds.size.y * 0.90f);
            var origin = (Vector2)bounds.center;

            var dir = new Vector2(Mathf.Sign(directionX), 0f);

            // ContactFilter 구성(벽 레이어 적용)
            var filter = CompatContactFilter2D.CreateNoFilter();
            filter.useLayerMask = true;
            filter.SetLayerMask(_wallMask);

            int count = CompatPhysics2D.BoxCastNonAlloc(
                origin,
                size,
                0f,
                dir,
                filter,
                _hits,
                _checkDistance
            );

            // 자기 자신/트리거 등의 예외는 필요 시 여기서 필터링
            // BoxCastNonAlloc 결과는 순서가 보장되지 않으므로, 가장 가까운 히트를 선택합니다.
            float bestDist = float.PositiveInfinity;
            RaycastHit2D bestHit = default;
            bool found = false;
            // 겹침/현재 접촉 노이즈 제거용 (0~아주 작은 값은 제외)
            const float kEpsilon = 0.001f;

            for (int i = 0; i < count; i++)
            {
                var h = _hits[i];
                if (!h.collider) continue;

                // Self-hit(자기 콜라이더) 및 겹침/노이즈(distance<=0) 무시
                if (h.collider == _col) continue;
                if (ignoreSelfHit && h.distance <= kEpsilon) continue;

                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    bestHit = h;
                    found = true;
                }
            }

            if (found)
            {
                return new WallHit
                {
                    IsHit = true,
                    Normal = bestHit.normal,
                    Point = bestHit.point,
                    Collider = bestHit.collider
                };
            }

            return default;
        }

        /// <summary>
        /// 지정한 방향 벡터로 벽을 검사합니다.
        /// - Jump 등에서 각도(wallJumpAngleDeg) + 거리(wallJumpPredictDistance) 기반의 반대편 벽 탐지에 사용합니다.
        /// - Cast 결과 중 distance가 매우 작은(겹침/현재 접촉) 히트는 제외하여 현재 벽이 먼저 잡히는 문제를 줄입니다.
        /// </summary>
        /// <param name="direction">월드 방향(정규화되지 않아도 됨)</param>
        /// <param name="distance">검사 거리</param>
        /// <param name="originOffset">레이/캐스트 시작점을 direction 방향으로 오프셋(콜라이더 내부 시작 방지)</param>
        public WallHit CheckDirection(Vector2 direction, float distance, float originOffset = 0f)
        {
            if (_col == null) return default;

            var dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

            // CapsuleCollider2D.bounds 기반 BoxCast
            var bounds = _col.bounds;
            var size = new Vector2(bounds.size.x * 0.92f, bounds.size.y * 0.90f);

            // 시작점 오프셋(현재 벽 내부에서 레이가 시작되며 distance=0으로 히트되는 케이스 완화)
            var origin = (Vector2)bounds.center + dir * originOffset;

            var filter = CompatContactFilter2D.CreateNoFilter();
            filter.useLayerMask = true;
            filter.SetLayerMask(_wallMask);

            float castDistance = Mathf.Max(0.01f, distance);

            int count = CompatPhysics2D.BoxCastNonAlloc(
                origin,
                size,
                0f,
                dir,
                filter,
                _hits,
                castDistance
            );

            float bestDist = float.PositiveInfinity;
            RaycastHit2D bestHit = default;
            bool found = false;

            // 겹침/현재 접촉 노이즈 제거용 (0~아주 작은 값은 제외)
            const float kEpsilon = 0.001f;

            for (int i = 0; i < count; i++)
            {
                var h = _hits[i];
                if (!h.collider) continue;

                if (h.collider == _col) continue;
                if (h.distance <= kEpsilon) continue;

                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    bestHit = h;
                    found = true;
                }
            }

            if (found)
            {
                return new WallHit
                {
                    IsHit = true,
                    Normal = bestHit.normal,
                    Point = bestHit.point,
                    Collider = bestHit.collider
                };
            }

            return default;
        }

    }
}