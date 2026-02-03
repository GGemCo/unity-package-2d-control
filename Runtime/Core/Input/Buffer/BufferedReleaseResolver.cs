using System;

namespace GGemCo2DControl
{
    /// <summary>
    /// 모든 액션을 "버튼 릴리즈"로 정규화하기 위한 80ms 입력 버퍼(Chord) 해석기입니다.
    /// 
    /// 규칙
    /// - Press 시점에 최대 waitWindowSeconds 동안 입력을 유지
    /// - waitWindowSeconds 이내에 Release가 오면 즉시 확정
    /// - Release가 없으면 deadline에서 Virtual Release로 확정
    /// - 대기 중 다른 버튼 Press가 오면 동일 그룹(Chord)으로 묶음
    /// </summary>
    internal sealed class BufferedReleaseResolver
    {
        public event Action<ResolvedButtonChord> Resolved;

        private float _waitWindowSeconds;

        // 활성 윈도우(단일)
        private bool _active;
        private float _openTime;
        private float _deadline;
        private PlayerButtonSet _pressed;
        private PlayerButtonSet _released;
        private int _virtualReleaseMask;

        public BufferedReleaseResolver(float waitWindowSeconds)
        {
            SetWaitWindowSeconds(waitWindowSeconds);
        }

        public void SetWaitWindowSeconds(float seconds)
        {
            // 과도한 값 방지(실수 입력 방어)
            if (seconds < 0.001f) seconds = 0.001f;
            // else if (seconds > 0.5f) seconds = 0.5f;
            _waitWindowSeconds = seconds;
        }

        public void PushPress(PlayerButtonId button, float time)
        {
            // 새 윈도우 시작
            if (!_active)
            {
                BeginWindow(button, time);
                return;
            }

            // 윈도우 유효 시간 안이면 chord로 결합
            if (time <= _deadline)
            {
                _pressed = _pressed.Add(button);
                return;
            }

            // 이미 만료된 윈도우이면 먼저 마감 후 새 윈도우 시작
            ResolveByTimeout(_deadline);
            BeginWindow(button, time);
        }

        public void PushRelease(PlayerButtonId button, float time)
        {
            if (!_active) return;
            if (!_pressed.Contains(button)) return;

            _released = _released.Add(button);

            // 단, chord 확정 정책은 "모든 pressed가 release"일 때 확정(권장 기본)
            if (_released.Mask == _pressed.Mask)
            {
                EmitResolved(time);
            }
        }

        /// <summary>
        /// 매 프레임 호출하여 deadline을 넘긴 입력을 Virtual Release로 확정합니다.
        /// </summary>
        public void Tick(float time)
        {
            if (!_active) return;
            if (time < _deadline) return;
            ResolveByTimeout(_deadline);
        }

        private void BeginWindow(PlayerButtonId first, float time)
        {
            _active = true;
            _openTime = time;
            _deadline = time + _waitWindowSeconds;
            _pressed = PlayerButtonSet.Empty.Add(first);
            _released = PlayerButtonSet.Empty;
            _virtualReleaseMask = 0;
        }

        private void ResolveByTimeout(float resolvedTime)
        {
            // 아직 릴리즈되지 않은 pressed는 Virtual Release로 간주
            int notReleasedMask = _pressed.Mask & ~_released.Mask;
            _virtualReleaseMask |= notReleasedMask;
            _released = new PlayerButtonSet(_pressed.Mask); // 전부 릴리즈 처리
            EmitResolved(resolvedTime);
        }

        private void EmitResolved(float time)
        {
            if (!_active) return;

            var chord = new ResolvedButtonChord(_pressed, _virtualReleaseMask, time);
            ResetWindow();
            Resolved?.Invoke(chord);
        }

        private void ResetWindow()
        {
            _active = false;
            _openTime = 0f;
            _deadline = 0f;
            _pressed = PlayerButtonSet.Empty;
            _released = PlayerButtonSet.Empty;
            _virtualReleaseMask = 0;
        }
    }
}
