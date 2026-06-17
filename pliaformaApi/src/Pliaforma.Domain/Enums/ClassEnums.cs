namespace Pliaforma.Domain.Enums;

/// <summary>Dersin yapıldığı ekipman.</summary>
public enum EquipmentType
{
    Mat = 0,
    Reformer = 1,
    Cadillac = 2,
    Chair = 3,
    Barrel = 4,
}

/// <summary>Ders kapasite tipi. MaxCapacity bu tipe göre doğrulanır.</summary>
public enum ClassCapacityType
{
    /// <summary>Birebir (kapasite 1).</summary>
    Private = 0,

    /// <summary>İkili (kapasite 2).</summary>
    Duo = 1,

    /// <summary>Grup (kapasite ≥ 2).</summary>
    Group = 2,
}

/// <summary>Ders seviyesi.</summary>
public enum ClassLevel
{
    Beginner = 0,
    Intermediate = 1,
    Advanced = 2,
    AllLevels = 3,
}

/// <summary>Dersin amacı/odağı.</summary>
public enum ClassPurpose
{
    General = 0,
    ClinicalRehab = 1,
    Prenatal = 2,
}

/// <summary>Hoca ücretinin nasıl hesaplandığı.</summary>
public enum RateType
{
    /// <summary>Ders (seans) başına.</summary>
    PerSession = 0,

    /// <summary>Saat başına.</summary>
    PerHour = 1,

    /// <summary>Ders başına öğrenci başına.</summary>
    PerStudentPerSession = 2,
}
