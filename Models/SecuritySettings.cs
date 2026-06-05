namespace SeatShuffler.Models;

/// <summary>앱 보안 설정(영속). 제약 탭 PIN 잠금 등.</summary>
public sealed class SecuritySettings
{
    /// <summary>제약 탭 PIN의 해시(SHA-256). null이면 아직 설정 안 됨.</summary>
    public string? ConstraintsPinHash { get; set; }
}
