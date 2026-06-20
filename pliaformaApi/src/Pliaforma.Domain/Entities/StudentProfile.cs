using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Bir stüdyodaki öğrenci kaydı. **User'dan ayrıdır:** misafir öğrenci (UserId null) hesapsız
/// oluşturulabilir; sonradan gerçek User'a bağlanır. İleride rezervasyon/ödeme/yoklama kayıtları
/// bu profilin Id'sine referans verecek — bu yüzden dönüşümde profil SİLİNMEZ, yalnızca UserId/
/// StudioMembershipId bağlanır (Id korunur).
/// </summary>
public class StudentProfile : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    /// <summary>null → misafir; dolu → gerçek kullanıcıya bağlı öğrenci.</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Dönüşümde bağlanan stüdyo üyeliği (Student).</summary>
    public Guid? StudioMembershipId { get; set; }
    public StudioMembership? StudioMembership { get; set; }

    public string FullName { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public StudentStatus Status { get; set; } = StudentStatus.Guest;
    public StudentSource Source { get; set; } = StudentSource.Other;

    /// <summary>Pilates seviyesi (Sprint 28A — öğrenci özelliği). Opsiyonel; program oluştururken seviye filtresi (28C) için.</summary>
    public StudentLevel? Level { get; set; }

    public string? Notes { get; set; }

    /// <summary>Hassas sağlık bilgisi — yalnızca yetkili roller görür (Owner/StudioAdmin, oluşturan hoca, öğrencinin kendisi).</summary>
    public string? HealthNotes { get; set; }

    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public DateOnly? BirthDate { get; set; }

    /// <summary>Kaydı oluşturan kullanıcı (hoca veya yönetici). Hocanın erişim sınırı buna dayanır.</summary>
    public Guid CreatedByUserId { get; set; }

    public DateTime? ConvertedAt { get; set; }

    public bool IsActive { get; set; } = true;
}
