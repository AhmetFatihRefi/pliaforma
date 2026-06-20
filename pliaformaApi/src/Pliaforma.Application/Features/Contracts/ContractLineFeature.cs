using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Contracts;

// ---------------------------------------------------------------------------------------------------
// Sprint 28A — Sözleşme satırı (StudentContractLine) READ-ONLY model. Yazma/line-ekleme 28B; bu turda yalnız
// backfill + okuma. Paid/Remaining STORED DEĞİL → ödenen = line'a bağlı Paid payment toplamı; kalan = TotalPrice − ödenen.
// ---------------------------------------------------------------------------------------------------

/// <summary>Sözleşme satırı görünümü (türetilen scheduled/available/paid/remaining dahil).</summary>
public sealed record StudentContractLineDto(
    Guid Id,
    Guid StudentContractId,
    Guid StudentProfileId,
    string StudentName,
    Guid ClassTypeId,
    string ClassTypeName,
    Guid InstructorProfileId,
    string InstructorName,
    DateOnly StartDate,
    DateOnly EndDate,
    int LessonCount,
    int ScheduledLessonCount,
    int AvailableLessonCount,
    decimal PricePerLesson,
    decimal TotalPrice,
    decimal Paid,
    decimal Remaining,
    int MakeupAllowance,
    DateOnly? DueDate,
    bool IsIndividual,
    StudentContractLineStatus Status,
    string Currency,
    int SequenceNo);

internal static class ContractLineProjection
{
    public static System.Linq.Expressions.Expression<Func<StudentContractLine, StudentContractLineDto>> ToDto(IAppDbContext db) =>
        l => new StudentContractLineDto(
            l.Id, l.StudentContractId, l.StudentContract!.StudentProfileId, l.StudentContract.StudentProfile!.FullName,
            l.ClassTypeId, l.ClassType!.Name, l.InstructorProfileId, l.InstructorProfile!.DisplayName,
            l.StartDate, l.EndDate, l.LessonCount,
            // ScheduledLessonCount: bu line'a bağlı aktif (Booked) Regular rezervasyon.
            db.ClassBookings.Count(b => b.StudentContractLineId == l.Id
                && b.Status == ClassBookingStatus.Booked && b.ClassSession!.SessionKind == ClassSessionKind.Regular),
            l.LessonCount - db.ClassBookings.Count(b => b.StudentContractLineId == l.Id
                && b.Status == ClassBookingStatus.Booked && b.ClassSession!.SessionKind == ClassSessionKind.Regular),
            l.PricePerLesson, l.TotalPrice,
            // Paid: line'a bağlı Paid payment toplamı.
            db.Payments.Where(p => p.StudentContractLineId == l.Id && p.Status == PaymentStatus.Paid)
                .Sum(p => (decimal?)p.Amount) ?? 0m,
            l.TotalPrice - (db.Payments.Where(p => p.StudentContractLineId == l.Id && p.Status == PaymentStatus.Paid)
                .Sum(p => (decimal?)p.Amount) ?? 0m),
            l.MakeupAllowance, l.DueDate, l.IsIndividual, l.Status, l.Currency, l.SequenceNo);
}

// ---- Staff: bir öğrencinin sözleşme satırları (Owner/StudioAdmin) ----
public sealed record GetStudentContractLinesQuery(Guid StudentProfileId) : IQuery<IReadOnlyList<StudentContractLineDto>>;

public sealed class GetStudentContractLinesHandler : IQueryHandler<GetStudentContractLinesQuery, IReadOnlyList<StudentContractLineDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetStudentContractLinesHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<IReadOnlyList<StudentContractLineDto>> Handle(GetStudentContractLinesQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        return await _db.StudentContractLines.AsNoTracking()
            .Where(l => l.StudioId == studioId && l.StudentContract!.StudentProfileId == request.StudentProfileId)
            .OrderByDescending(l => l.StartDate).ThenBy(l => l.SequenceNo)
            .Select(ContractLineProjection.ToDto(_db))
            .ToListAsync(ct);
    }
}

// ---- Student self: kendi sözleşme satırları (salt-okunur) ----
public sealed record GetMyContractLinesQuery : IQuery<IReadOnlyList<StudentContractLineDto>>;

public sealed class GetMyContractLinesHandler : IQueryHandler<GetMyContractLinesQuery, IReadOnlyList<StudentContractLineDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetMyContractLinesHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<IReadOnlyList<StudentContractLineDto>> Handle(GetMyContractLinesQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var profileId = await _db.StudentProfiles
            .Where(s => s.StudioId == studioId && s.UserId == userId)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        if (profileId is null)
            return System.Array.Empty<StudentContractLineDto>();

        return await _db.StudentContractLines.AsNoTracking()
            .Where(l => l.StudioId == studioId && l.StudentContract!.StudentProfileId == profileId)
            .OrderByDescending(l => l.StartDate).ThenBy(l => l.SequenceNo)
            .Select(ContractLineProjection.ToDto(_db))
            .ToListAsync(ct);
    }
}
