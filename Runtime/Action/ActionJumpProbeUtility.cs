using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// ActionJump에서 사용하는 접지/천장 판정 박스 계산을 공용화하는 유틸리티입니다.
    /// </summary>
    public static class ActionJumpProbeUtility
    {
        /// <summary>
        /// 발 아래 접지 판정 박스 크기를 계산합니다.
        /// </summary>
        public static Vector2 BuildGroundProbeSize(Bounds bounds, float widthScale, float height)
        {
            float width = Mathf.Max(0.02f, bounds.size.x * Mathf.Clamp(widthScale, 0.1f, 1f));
            float clampedHeight = Mathf.Max(0.01f, height);
            return new Vector2(width, clampedHeight);
        }

        /// <summary>
        /// 발 아래 접지 판정 박스 중심점을 계산합니다.
        /// </summary>
        
        public static Vector2 BuildGroundProbeCenter(Bounds bounds, float extraDistance, Vector2 size)
        {
            float clampedDistance = Mathf.Max(0f, extraDistance);
            return new Vector2(bounds.center.x, bounds.min.y - clampedDistance - (size.y * 0.5f));
        }

        /// <summary>
        /// 머리 위 천장 판정 박스 크기를 계산합니다.
        /// </summary>
        public static Vector2 BuildCeilingProbeSize(Bounds bounds, float widthScale, float height)
        {
            float width = Mathf.Max(0.02f, bounds.size.x * Mathf.Clamp(widthScale, 0.1f, 1f));
            float clampedHeight = Mathf.Max(0.01f, height);
            return new Vector2(width, clampedHeight);
        }

        /// <summary>
        /// 머리 위 천장 판정 박스 중심점을 계산합니다.
        /// </summary>
        public static Vector2 BuildCeilingProbeCenter(Bounds bounds, float extraDistance, Vector2 size)
        {
            float clampedDistance = Mathf.Max(0f, extraDistance);
            return new Vector2(bounds.center.x, bounds.max.y + clampedDistance + (size.y * 0.5f));
        }
    }
}
