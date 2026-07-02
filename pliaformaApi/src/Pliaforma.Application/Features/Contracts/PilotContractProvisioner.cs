using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Contracts;

/// <summary>
/// Sprint 46 — Pilot Simple People Mode. Ad-soyad (misafir) öğrencinin Regular derse eklenebilmesi için
/// GİZLİ bir pilot contract/line'ı LAZY (booking anında) bulur veya oluşturur. Amaç: mevcut booking/attendance/
/// move/kapasite/çakışma zincirini HİÇ bozmadan (null-line yoluna gitmeden) gerçek bir line ile devam etmek.
///
/// Pilot line: <c>IsPilot=true</c>, TotalPrice=0, DueDate=null (borç/gecikme üretmez), çok büyük LessonCount
/// (pratikte sınırsız), kapasite/hoca tercihi yok (herhangi ders/hoca), geniş tarih aralığı (backfill dâhil).
/// Finans/sözleşme/rapor query'lerinde gizlidir (ContractLineQuery.BuildAsync + resolver <c>!IsPilot</c> filtresi).
/// Yalnız bu yoldan kullanılır → flag kapansa da yeni booking'lerde auto-resolve etmez.
/// </summary>
public sealed class PilotContractProvisioner
{
    private readonly IAppDbContext _db;

    public PilotContractProvisioner(IAppDbContext db) => _db = db;

    // Pilot line hak/tarih sınırlaması taşımaz: geniş aralık + çok büyük ders sayısı.
    private const int PilotLessonCount = 100_000;
    private static readonly DateOnly PilotStart = new(2000, 1, 1);
    private static readonly DateOnly PilotEnd = new(2999, 12, 31);

    /// <summary>
    /// Verilen öğrenci + seansın ders tipi için AKTİF pilot line'ı bulur; yoksa (gerekirse pilot header'ı da açarak)
    /// oluşturur ve context'e ekler (kaydetmez — çağıran BookingCreator ile aynı transaction'da yazılır).
    /// </summary>
    public async Task<StudentContractLine> ResolveOrCreatePilotLineAsync(
        Guid studioId, StudentProfile student, ClassSession session, Guid createdByUserId, CancellationToken ct)
    {
        var existing = await _db.StudentContractLines
            .Include(l => l.StudentContract)
            .FirstOrDefaultAsync(l => l.StudioId == studioId
                && l.IsPilot
                && l.Status == StudentContractLineStatus.Active
                && l.ClassTypeId == session.ClassTypeId
                && l.StudentContract!.StudentProfileId == student.Id, ct);
        if (existing is not null)
            return existing;

        var header = await _db.StudentContracts
            .FirstOrDefaultAsync(c => c.StudioId == studioId && c.IsPilot
                && c.StudentProfileId == student.Id && c.Status == StudentContractStatus.Active, ct);
        if (header is null)
        {
            header = new StudentContract
            {
                StudioId = studioId,
                StudentProfileId = student.Id,
                ClassTypeId = session.ClassTypeId,
                InstructorProfileId = null,
                StartDate = PilotStart,
                EndDate = PilotEnd,
                TotalLessonCount = PilotLessonCount,
                MakeupAllowance = 0,
                Price = 0m,
                Currency = "TRY",
                IsIndividualDefault = false,
                Status = StudentContractStatus.Active,
                Notes = "[PILOT] Otomatik oluşturulan pilot sözleşmesi (Sprint 46).",
                CreatedByUserId = createdByUserId,
                IsPilot = true,
            };
            _db.StudentContracts.Add(header);
        }

        var line = new StudentContractLine
        {
            StudioId = studioId,
            StudentContractId = header.Id,
            ClassTypeId = session.ClassTypeId,
            InstructorProfileId = null,
            PreferredMaxCapacity = null,
            StartDate = PilotStart,
            EndDate = PilotEnd,
            LessonCount = PilotLessonCount,
            PricePerLesson = 0m,
            TotalPrice = 0m,
            MakeupAllowance = 0,
            DueDate = null,
            IsIndividual = false,
            Status = StudentContractLineStatus.Active,
            Currency = "TRY",
            SequenceNo = 1,
            Notes = "[PILOT] Otomatik oluşturulan pilot kaydı (Sprint 46).",
            CreatedByUserId = createdByUserId,
            IsPilot = true,
        };
        _db.StudentContractLines.Add(line);
        return line;
    }
}
