namespace Pliaforma.Domain.Enums;

/// <summary>Öğrenci kaydının yaşam döngüsü durumu.</summary>
public enum StudentStatus
{
    /// <summary>Misafir — henüz bir User hesabına bağlı değil (UserId null).</summary>
    Guest = 0,

    /// <summary>Aktif — gerçek kullanıcıya bağlı.</summary>
    Active = 1,

    /// <summary>Pasif — devre dışı bırakılmış (soft delete değil).</summary>
    Inactive = 2,
}

/// <summary>
/// Öğrenci pilates seviyesi (Sprint 28A — seviye artık öğrencinin özelliği; ders tipinin değil).
/// Opsiyonel; program oluştururken aynı seviyeden öğrencileri bir araya getirme filtresi (28C) için kullanılır.
/// </summary>
public enum StudentLevel
{
    /// <summary>Başlangıç.</summary>
    Beginner = 0,

    /// <summary>Orta.</summary>
    Intermediate = 1,

    /// <summary>İleri.</summary>
    Advanced = 2,
}

/// <summary>Öğrencinin stüdyoya nasıl ulaştığı/kaydedildiği.</summary>
public enum StudentSource
{
    /// <summary>Telefonla aradı.</summary>
    Phone = 0,

    /// <summary>Kapıdan geldi.</summary>
    WalkIn = 1,

    /// <summary>Web üzerinden.</summary>
    Web = 2,

    /// <summary>Tavsiye ile.</summary>
    Referral = 3,

    Other = 4,
}
