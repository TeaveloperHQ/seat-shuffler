namespace SeatShuffler.Models;

/// <summary>짝(인접 2칸) 구성 방식. 열 수가 2 이상일 때만 의미가 있다.</summary>
public enum PairMode
{
    /// <summary>동성짝 — 짝의 두 명이 같은 성별.</summary>
    SameGender = 0,
    /// <summary>이성짝 — 짝의 두 명이 다른 성별(둘 다 성별이 지정되어야 함).</summary>
    OppositeGender = 1,
}
