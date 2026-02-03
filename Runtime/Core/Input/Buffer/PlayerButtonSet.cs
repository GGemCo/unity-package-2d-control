using System;

namespace GGemCo2DControl
{
    /// <summary>
    /// 버튼 조합(순서 무관)을 비트 마스크로 표현합니다.
    /// - 단일 버튼/동시 입력(Chord) 모두 표현 가능
    /// </summary>
    internal readonly struct PlayerButtonSet : IEquatable<PlayerButtonSet>
    {
        private readonly int _mask;

        public PlayerButtonSet(int mask)
        {
            _mask = mask;
        }

        public static PlayerButtonSet Empty => new PlayerButtonSet(0);

        public int Mask => _mask;

        public bool IsEmpty => _mask == 0;

        public bool Contains(PlayerButtonId button)
        {
            int bit = 1 << (int)button;
            return (_mask & bit) != 0;
        }

        public PlayerButtonSet Add(PlayerButtonId button)
        {
            int bit = 1 << (int)button;
            return new PlayerButtonSet(_mask | bit);
        }

        public PlayerButtonSet Remove(PlayerButtonId button)
        {
            int bit = 1 << (int)button;
            return new PlayerButtonSet(_mask & ~bit);
        }

        public static PlayerButtonSet From(PlayerButtonId button)
        {
            return new PlayerButtonSet(1 << (int)button);
        }

        public bool Equals(PlayerButtonSet other) => _mask == other._mask;
        public override bool Equals(object obj) => obj is PlayerButtonSet other && Equals(other);
        public override int GetHashCode() => _mask;
        public static bool operator ==(PlayerButtonSet a, PlayerButtonSet b) => a.Equals(b);
        public static bool operator !=(PlayerButtonSet a, PlayerButtonSet b) => !a.Equals(b);

        public override string ToString()
        {
            return $"PlayerButtonSet(mask=0x{_mask:X})";
        }
    }
}
