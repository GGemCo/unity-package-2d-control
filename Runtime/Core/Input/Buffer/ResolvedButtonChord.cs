namespace GGemCo2DControl
{
    /// <summary>
    /// 80ms 입력 버퍼(Press → Release 정규화) 결과입니다.
    /// - 동시에 눌린 버튼들을 하나의 Set으로 묶습니다.
    /// - 실제 릴리즈가 없었던 버튼은 VirtualReleaseMask에 포함됩니다.
    /// </summary>
    internal readonly struct ResolvedButtonChord
    {
        public readonly PlayerButtonSet Buttons;
        public readonly int VirtualReleaseMask;
        public readonly float ResolvedTime;

        public ResolvedButtonChord(PlayerButtonSet buttons, int virtualReleaseMask, float resolvedTime)
        {
            Buttons = buttons;
            VirtualReleaseMask = virtualReleaseMask;
            ResolvedTime = resolvedTime;
        }

        public bool IsVirtual(PlayerButtonId button)
        {
            int bit = 1 << (int)button;
            return (VirtualReleaseMask & bit) != 0;
        }
    }
}
