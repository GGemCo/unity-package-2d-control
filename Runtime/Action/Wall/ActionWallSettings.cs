using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// <see cref="ActionWall"/>의 설정값 캐시 및 애니메이션/레이어 구성을 담당하는 partial 구간입니다.
    /// </summary>
    /// <remarks>
    /// - 설정 원본은 <c>playerActionSettings</c>(<see cref="GGemCoPlayerActionSettings"/>)이며,
    ///   런타임에서 빠르게 접근할 수 있도록 필드로 캐시합니다.
    /// - 센서(<c>_sensor</c>)의 레이어/거리 구성도 여기서 동기화합니다.
    /// </remarks>
    public partial class ActionWall
    {
        // --- Settings (cached from GGemCoPlayerActionSettings) ---

        /// <summary>Hang 상태에서 입력이 없을 때 Slide로 전환되기까지의 지연 시간(초)입니다.</summary>
        internal float HangToSlideDelay => _hangToSlideDelay;

        /// <summary>벽 점프 직후 재부착을 금지하는 쿨다운 시간(초)입니다.</summary>
        internal float ReattachCooldown => _reattachCooldown;

        /// <summary>벽에 매달릴 때 X축으로 벽 안쪽으로 파고드는(스냅) 보정값입니다.</summary>
        internal float WallHangInsetX => _wallHangInsetX;

        /// <summary>Slide 상태에서 아래로 떨어지는(미끄러지는) 속도입니다.</summary>
        internal float SlideDownSpeed => _slideDownSpeed;

        /// <summary>SlideEnd 종료 시 인계할 점프/탈출 속도의 X 성분입니다.</summary>
        internal float SlideEndExitJumpX => _slideEndExitJumpX;

        /// <summary>SlideEnd 종료 시 인계할 점프/탈출 속도의 Y 성분입니다.</summary>
        internal float SlideEndExitJumpY => _slideEndExitJumpY;

        /// <summary>벽 점프 방향을 구성할 각도(도)입니다. (2D 기준: 0°=+X, 90°=+Y)</summary>
        internal float WallJumpAngleDeg => _wallJumpAngleDeg;

        /// <summary>반대편 벽 목표가 존재할 때 사용할 벽 점프 속도입니다.</summary>
        internal float WallJumpSpeedWithOppositeWall => _wallJumpSpeedWithWall;

        /// <summary>반대편 벽 목표를 찾기 위한 예측 레이 길이(거리)입니다.</summary>
        internal float WallJumpPredictDistance => _wallJumpPredictDistance;

        /// <summary>벽 점프 Kinematic 시뮬레이션의 최대 지속 시간(초)입니다.</summary>
        internal float WallJumpMaxDuration => _wallJumpMaxDuration;

        /// <summary>JumpEnd 전환/종료를 판단할 목표 벽 접근 거리(또는 종료 판정 거리)입니다.</summary>
        internal float WallJumpEndDistance => _wallJumpEndDistance;

        /// <summary>JumpEnd 종료 시 인계할 점프/탈출 속도의 X 성분입니다.</summary>
        internal float WallJumpEndExitJumpX => _wallJumpEndExitJumpX;

        /// <summary>JumpEnd 종료 시 인계할 점프/탈출 속도의 Y 성분입니다.</summary>
        internal float WallJumpEndExitJumpY => _wallJumpEndExitJumpY;

        /// <summary>벽 판정에 사용할 레이어 마스크입니다.</summary>
        internal LayerMask WallMask => _wallMask;

        /// <summary>벽 판정(센서 체크/레이캐스트)에 사용할 거리입니다.</summary>
        internal float WallCheckDistance => _wallCheckDistance;

        // Anim

        /// <summary>Hang 애니메이션 이름(프리픽스 포함)입니다.</summary>
        internal string AnimHang => _animHang;

        /// <summary>Slide 애니메이션 이름(프리픽스 포함)입니다.</summary>
        internal string AnimSlide => _animSlide;

        /// <summary>WallJump 애니메이션 이름(프리픽스 포함)입니다.</summary>
        internal string AnimWallJump => _animWallJump;

        /// <summary>Hang 애니메이션이 실제로 존재하는지 여부입니다.</summary>
        internal bool HasHangAnim => _hasHang;

        /// <summary>Slide 애니메이션이 실제로 존재하는지 여부입니다.</summary>
        internal bool HasSlideAnim => _hasSlide;

        /// <summary>WallJump 애니메이션이 실제로 존재하는지 여부입니다.</summary>
        internal bool HasWallJumpAnim => _hasWallJump;

        /// <summary>
        /// Hang 에셋의 기본 바라보는 방향(리소스 기준)입니다.
        /// </summary>
        /// <remarks>
        /// 벽 위치(<c>wallSideX</c>)와 리소스 방향(<see cref="HangAssetFacingX"/>)을 비교하여
        /// 플립 여부를 결정합니다. (예: <c>wallSideX == assetFacingX</c>이면 Flip)
        /// </remarks>
        internal int HangAssetFacingX => _hangAssetFacingX;

        /// <summary>
        /// 플레이어 액션 설정(<c>playerActionSettings</c>)을 캐시하고, 센서/애니메이션 존재 여부를 동기화합니다.
        /// </summary>
        /// <remarks>
        /// - 벽 레이어 마스크가 0이면 기본값(타일맵 지형 레이어)을 사용합니다.
        /// - 애니메이션 프리픽스는 비어있으면 기본 문자열(wall_hang/wall_slide/wall_jump)을 사용합니다.
        /// </remarks>
        protected override void ApplySettings()
        {
            if (!playerActionSettings) return;

            _enabled = playerActionSettings.enableWallAction;

            _hangToSlideDelay = playerActionSettings.wallHangToSlideDelay;
            _reattachCooldown = playerActionSettings.wallReattachCooldown;
            _wallHangInsetX = playerActionSettings.wallHangInsetX;
            _slideDownSpeed = playerActionSettings.wallSlideDownSpeed;

            _slideEndExitJumpX = playerActionSettings.wallSlideEndExitJumpX;
            _slideEndExitJumpY = playerActionSettings.wallSlideEndExitJumpY;

            _wallJumpAngleDeg = playerActionSettings.wallJumpAngleDeg;
            _wallJumpSpeedWithWall = playerActionSettings.wallJumpSpeedWithOppositeWall;
            _wallJumpPredictDistance = playerActionSettings.wallJumpPredictDistance;
            _wallJumpMaxDuration = playerActionSettings.wallJumpMaxDuration;
            _wallJumpEndDistance = playerActionSettings.wallJumpEndDistance;
            _wallJumpEndExitJumpX = playerActionSettings.wallJumpEndExitJumpX;
            _wallJumpEndExitJumpY = playerActionSettings.wallJumpEndExitJumpY;

            _wallMask = playerActionSettings.wallMask;
            if (_wallMask == 0)
                _wallMask = LayerMask.GetMask(ConfigLayer.GetValue(ConfigLayer.Keys.TileMapGround));

            _wallCheckDistance = playerActionSettings.wallCheckDistance;

            // 센서가 이미 생성되어 있다는 전제(Initialize 이후 ApplySettings 호출)를 기반으로 구성값을 반영합니다.
            _sensor.Configure(_wallMask, _wallCheckDistance);

            _hangAssetFacingX =
                playerActionSettings.wallHangAssetFacing == GGemCoPlayerActionSettings.HangAnimationAssetFacing.Left
                    ? -1
                    : 1;

            // Animation prefixes
            _animHang = !string.IsNullOrWhiteSpace(playerActionSettings.prefixWallHangAnimation)
                ? playerActionSettings.prefixWallHangAnimation
                : "wall_hang";
            _animSlide = !string.IsNullOrWhiteSpace(playerActionSettings.prefixWallSlideAnimation)
                ? playerActionSettings.prefixWallSlideAnimation
                : "wall_slide";
            _animWallJump = !string.IsNullOrWhiteSpace(playerActionSettings.prefixWallJumpAnimation)
                ? playerActionSettings.prefixWallJumpAnimation
                : "wall_jump";

            var anim = actionCharacterBase.CharacterAnimationController;
            _hasHang = anim != null && anim.HasAnimation(_animHang);
            _hasSlide = anim != null && anim.HasAnimation(_animSlide);
            _hasWallJump = anim != null && anim.HasAnimation(_animWallJump);
        }

        // backing fields

        /// <summary>벽 액션 활성화 여부입니다.</summary>
        private bool _enabled;

        private float _hangToSlideDelay;
        private float _reattachCooldown;
        private float _wallHangInsetX;
        private float _slideDownSpeed;

        private float _slideEndExitJumpX;
        private float _slideEndExitJumpY;

        private float _wallJumpAngleDeg;
        private float _wallJumpSpeedWithWall;
        private float _wallJumpPredictDistance;
        private float _wallJumpMaxDuration;
        private float _wallJumpEndDistance;
        private float _wallJumpEndExitJumpX;
        private float _wallJumpEndExitJumpY;

        private LayerMask _wallMask;
        private float _wallCheckDistance;

        private string _animHang;
        private string _animSlide;
        private string _animWallJump;
        private bool _hasHang;
        private bool _hasSlide;
        private bool _hasWallJump;

        /// <summary>Hang 애니메이션 에셋의 기본 방향(-1: Left, +1: Right)입니다.</summary>
        private int _hangAssetFacingX;
    }
}
