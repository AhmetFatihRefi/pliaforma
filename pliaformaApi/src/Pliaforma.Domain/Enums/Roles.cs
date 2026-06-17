namespace Pliaforma.Domain.Enums;

/// <summary>
/// Kullanıcının platform genelindeki rolü. Stüdyo içi yetki StudioRole ile ayrı tutulur.
/// </summary>
public enum GlobalRole
{
    Standard = 0,
    PlatformAdmin = 1,
}

/// <summary>
/// Bir kullanıcının BELİRLİ bir stüdyodaki yetkisi (StudioMembership üzerinde taşınır).
/// Az sayıda rol: Owner/StudioAdmin her şeyi yönetir, Instructor/Student yalnızca kendini görür.
/// </summary>
public enum StudioRole
{
    /// <summary>Stüdyo sahibi hesabı. Tüm yetkiler.</summary>
    Owner = 0,

    /// <summary>Stüdyoyu yöneten kişi (hoca olmak zorunda değil). Tüm yönetim yetkileri.</summary>
    StudioAdmin = 1,

    /// <summary>Hoca. Yalnızca kendi programını/öğrencilerini görür.</summary>
    Instructor = 2,

    /// <summary>Öğrenci. Yalnızca kendi programını/devamsızlığını/performansını görür.</summary>
    Student = 3,
}

/// <summary>Üyeliğin yaşam döngüsü durumu.</summary>
public enum MembershipStatus
{
    /// <summary>Davet/başvuru bekliyor, henüz kabul edilmedi.</summary>
    Pending = 0,

    /// <summary>Aktif üyelik; stüdyoya erişebilir.</summary>
    Active = 1,

    /// <summary>Davet/başvuru reddedildi.</summary>
    Rejected = 2,

    /// <summary>Stüdyodan çıkarıldı; erişim kapalı.</summary>
    Removed = 3,
}

/// <summary>Üyeliğin nasıl başladığı (kim başlattı).</summary>
public enum MembershipDirection
{
    /// <summary>Stüdyo kullanıcıyı davet etti.</summary>
    InvitedByStudio = 0,

    /// <summary>Kullanıcı stüdyoya katılmak için başvurdu.</summary>
    RequestedByUser = 1,
}
