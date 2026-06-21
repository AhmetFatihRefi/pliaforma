using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Contracts;

// ===================================================================================================
// Sprint 28A/28B — Sözleşme satırı (StudentContractLine). 28A: read-model + backfill. 28B: line ekle/edit/cancel +
// line-bazlı ödeme/borç. Paid/Remaining STORED DEĞİL.
//
//   paid(L) = Σ Payment(Paid, lineId=L)  +  ( header(L)'de TAM 1 line varsa: Σ Payment(Paid, lineId=NULL, contractId=L.header) )
//
// Legacy fallback YALNIZ tek-line header'da çalışır → çok-line header'da line'sız ödeme HİÇBİR line'a dağılmaz (çift sayım yok).
// ===================================================================================================

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

internal static class ContractLineQuery
{
    /// <summary>Verilen line sorgusunu legacy-aware paid ile DTO'ya çevirir (paid hesabı bellek-içi; çift sayım yok).</summary>
    public static async Task<List<StudentContractLineDto>> BuildAsync(IAppDbContext db, IQueryable<StudentContractLine> q, CancellationToken ct)
    {
        var raw = await q
            .OrderByDescending(l => l.StartDate).ThenBy(l => l.SequenceNo)
            .Select(l => new
            {
                l.Id, l.StudentContractId,
                StudentProfileId = l.StudentContract!.StudentProfileId,
                StudentName = l.StudentContract.StudentProfile!.FullName,
                l.ClassTypeId, ClassTypeName = l.ClassType!.Name,
                l.InstructorProfileId, InstructorName = l.InstructorProfile!.DisplayName,
                l.StartDate, l.EndDate, l.LessonCount,
                Scheduled = db.ClassBookings.Count(b => b.StudentContractLineId == l.Id
                    && b.Status == ClassBookingStatus.Booked && b.ClassSession!.SessionKind == ClassSessionKind.Regular),
                // Sprint 28C-1 (karar #7): legacy null-line booking — tek-line header'da bu line'a sayılır (available fallback).
                LegacyScheduled = db.ClassBookings.Count(b => b.StudentContractLineId == null
                    && b.StudentContractId == l.StudentContractId
                    && b.Status == ClassBookingStatus.Booked && b.ClassSession!.SessionKind == ClassSessionKind.Regular),
                l.PricePerLesson, l.TotalPrice,
                DirectPaid = db.Payments.Where(p => p.StudentContractLineId == l.Id && p.Status == PaymentStatus.Paid)
                    .Sum(p => (decimal?)p.Amount) ?? 0m,
                HeaderLineCount = db.StudentContractLines.Count(x => x.StudentContractId == l.StudentContractId),
                LegacyCandidate = db.Payments.Where(p => p.StudentContractLineId == null
                    && p.StudentContractId == l.StudentContractId && p.Status == PaymentStatus.Paid)
                    .Sum(p => (decimal?)p.Amount) ?? 0m,
                l.MakeupAllowance, l.DueDate, l.IsIndividual, l.Status, l.Currency, l.SequenceNo,
            })
            .ToListAsync(ct);

        return raw.Select(r =>
        {
            var paid = r.DirectPaid + (r.HeaderLineCount == 1 ? r.LegacyCandidate : 0m);
            var scheduled = r.Scheduled + (r.HeaderLineCount == 1 ? r.LegacyScheduled : 0);
            return new StudentContractLineDto(
                r.Id, r.StudentContractId, r.StudentProfileId, r.StudentName, r.ClassTypeId, r.ClassTypeName,
                r.InstructorProfileId, r.InstructorName, r.StartDate, r.EndDate, r.LessonCount, scheduled,
                r.LessonCount - scheduled, r.PricePerLesson, r.TotalPrice, paid, r.TotalPrice - paid,
                r.MakeupAllowance, r.DueDate, r.IsIndividual, r.Status, r.Currency, r.SequenceNo);
        }).ToList();
    }

    /// <summary>Line-bazlı borç özeti: currency rollup + line kalemleri + atanmamış (hiçbir line'a sayılmayan) Paid ödemeler.</summary>
    public static async Task<LineDebtSummaryDto> ComputeDebtAsync(IAppDbContext db, Guid studioId, Guid studentProfileId, CancellationToken ct)
    {
        var lines = await BuildAsync(db,
            db.StudentContractLines.AsNoTracking().Where(l => l.StudioId == studioId && l.StudentContract!.StudentProfileId == studentProfileId), ct);

        var byCurrency = lines
            .GroupBy(l => l.Currency)
            .Select(g => new CurrencyDebtDto(g.Key, g.Sum(x => x.TotalPrice), g.Sum(x => x.Paid), g.Sum(x => x.Remaining)))
            .OrderBy(g => g.Currency).ToList();

        // Atanmamış = (tüm Paid ödeme, currency) − (Σ line.paid, currency). Line.paid legacy fallback'i içerir → çift sayım yok.
        var paidByCur = await db.Payments.AsNoTracking()
            .Where(p => p.StudioId == studioId && p.StudentProfileId == studentProfileId && p.Status == PaymentStatus.Paid)
            .GroupBy(p => p.Currency)
            .Select(g => new { Currency = g.Key, Sum = g.Sum(x => x.Amount) })
            .ToListAsync(ct);
        var linePaidByCur = lines.GroupBy(l => l.Currency).ToDictionary(g => g.Key, g => g.Sum(x => x.Paid));
        var unassigned = paidByCur
            .Select(p => new CurrencyPaidDto(p.Currency, p.Sum - (linePaidByCur.TryGetValue(p.Currency, out var v) ? v : 0m)))
            .Where(u => u.Paid > 0m).OrderBy(u => u.Currency).ToList();

        return new LineDebtSummaryDto(byCurrency, lines, unassigned);
    }

    /// <summary>Header çözümü (28B): id verilirse aktif header'ı doğrula; yoksa öğrencinin en güncel aktif header'ı; o da yoksa null (çağıran yeni header açar).</summary>
    public static async Task<StudentContract?> ResolveActiveHeaderAsync(IAppDbContext db, Guid studioId, Guid studentProfileId, Guid? headerId, CancellationToken ct)
    {
        if (headerId is { } hid)
        {
            return await db.StudentContracts
                .FirstOrDefaultAsync(c => c.Id == hid && c.StudioId == studioId && c.StudentProfileId == studentProfileId, ct)
                ?? throw new NotFoundException("Sözleşme (header)", hid);
        }
        return await db.StudentContracts
            .Where(c => c.StudioId == studioId && c.StudentProfileId == studentProfileId && c.Status == StudentContractStatus.Active)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
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
        return await ContractLineQuery.BuildAsync(_db,
            _db.StudentContractLines.AsNoTracking().Where(l => l.StudioId == studioId && l.StudentContract!.StudentProfileId == request.StudentProfileId), ct);
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
        var profileId = await StudentSelfResolver.ResolveProfileIdAsync(_db, studioId, userId, ct);
        if (profileId is null) return System.Array.Empty<StudentContractLineDto>();

        return await ContractLineQuery.BuildAsync(_db,
            _db.StudentContractLines.AsNoTracking().Where(l => l.StudioId == studioId && l.StudentContract!.StudentProfileId == profileId), ct);
    }
}

// ---- Yeni line (ek alım) — header çözümü; TotalPrice=count×perLesson (override yok) ----
public sealed record CreateStudentContractLineCommand(
    Guid StudentProfileId,
    Guid? HeaderId,
    Guid ClassTypeId,
    Guid InstructorProfileId,
    DateOnly StartDate,
    DateOnly EndDate,
    int LessonCount,
    decimal PricePerLesson,
    int MakeupAllowance,
    DateOnly? DueDate,
    bool IsIndividual,
    string? Currency,
    string? Notes) : ICommand<StudentContractLineDto>, IStudioWriteRequest;

public sealed class CreateStudentContractLineValidator : AbstractValidator<CreateStudentContractLineCommand>
{
    public CreateStudentContractLineValidator()
    {
        RuleFor(x => x.StudentProfileId).NotEmpty();
        RuleFor(x => x.ClassTypeId).NotEmpty();
        RuleFor(x => x.InstructorProfileId).NotEmpty();
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("Bitiş tarihi başlangıçtan önce olamaz.");
        RuleFor(x => x.LessonCount).GreaterThan(0).LessThanOrEqualTo(1000);
        RuleFor(x => x.PricePerLesson).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MakeupAllowance).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1000);
        RuleFor(x => x.Currency).MaximumLength(8);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class CreateStudentContractLineHandler : ICommandHandler<CreateStudentContractLineCommand, StudentContractLineDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public CreateStudentContractLineHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<StudentContractLineDto> Handle(CreateStudentContractLineCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.StudentProfileId && s.StudioId == studioId, ct)
            ?? throw new NotFoundException("Öğrenci", request.StudentProfileId);
        if (student.Status == StudentStatus.Inactive)
            throw new BusinessRuleException("Pasif öğrenci için satır eklenemez.");

        var classType = await _db.ClassTypes.FirstOrDefaultAsync(c => c.Id == request.ClassTypeId && c.StudioId == studioId, ct)
            ?? throw new NotFoundException("Ders grubu", request.ClassTypeId);
        var instructor = await _db.InstructorProfiles.FirstOrDefaultAsync(p => p.Id == request.InstructorProfileId && p.StudioId == studioId, ct)
            ?? throw new NotFoundException("Hoca", request.InstructorProfileId);

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "TRY" : request.Currency.Trim().ToUpperInvariant();
        var totalPrice = decimal.Round(request.LessonCount * request.PricePerLesson, 2);

        // Header çözümü: verildiyse o; yoksa en güncel aktif; o da yoksa YENİ header (dormant alanları bu line ile).
        var header = await ContractLineQuery.ResolveActiveHeaderAsync(_db, studioId, student.Id, request.HeaderId, ct);
        if (header is null)
        {
            header = new StudentContract
            {
                StudioId = studioId,
                StudentProfileId = student.Id,
                ClassTypeId = classType.Id,
                InstructorProfileId = instructor.Id,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                TotalLessonCount = request.LessonCount,
                MakeupAllowance = request.MakeupAllowance,
                Price = totalPrice,
                Currency = currency,
                IsIndividualDefault = request.IsIndividual,
                Status = StudentContractStatus.Active,
                CreatedByUserId = userId,
            };
            _db.StudentContracts.Add(header);
        }

        var nextSeq = 1 + await _db.StudentContractLines
            .Where(l => l.StudentContractId == header.Id)
            .Select(l => (int?)l.SequenceNo).MaxAsync(ct) ?? 1;

        var line = new StudentContractLine
        {
            StudioId = studioId,
            StudentContractId = header.Id,
            ClassTypeId = classType.Id,
            InstructorProfileId = instructor.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            LessonCount = request.LessonCount,
            PricePerLesson = request.PricePerLesson,
            TotalPrice = totalPrice,
            MakeupAllowance = request.MakeupAllowance,
            DueDate = request.DueDate,
            IsIndividual = request.IsIndividual,
            Status = StudentContractLineStatus.Active,
            Currency = currency,
            SequenceNo = nextSeq,
            Notes = request.Notes?.Trim(),
            CreatedByUserId = userId,
        };
        _db.StudentContractLines.Add(line);
        await _db.SaveChangesAsync(ct);

        var dtos = await ContractLineQuery.BuildAsync(_db, _db.StudentContractLines.AsNoTracking().Where(l => l.Id == line.Id), ct);
        return dtos[0];
    }
}

// ---- Line edit (yalnız DueDate + Notes) ----
public sealed record UpdateContractLineCommand(Guid LineId, DateOnly? DueDate, string? Notes) : ICommand<StudentContractLineDto>, IStudioWriteRequest;

public sealed class UpdateContractLineValidator : AbstractValidator<UpdateContractLineCommand>
{
    public UpdateContractLineValidator() { RuleFor(x => x.LineId).NotEmpty(); RuleFor(x => x.Notes).MaximumLength(2000); }
}

public sealed class UpdateContractLineHandler : ICommandHandler<UpdateContractLineCommand, StudentContractLineDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public UpdateContractLineHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<StudentContractLineDto> Handle(UpdateContractLineCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var line = await _db.StudentContractLines.FirstOrDefaultAsync(l => l.Id == request.LineId && l.StudioId == studioId, ct)
            ?? throw new NotFoundException("Sözleşme satırı", request.LineId);

        // 28B: yalnız son ödeme tarihi + not değişebilir (ders sayısı/ücret/tarih yanlışsa yeni line açılır).
        line.DueDate = request.DueDate;
        line.Notes = request.Notes?.Trim();
        await _db.SaveChangesAsync(ct);

        var dtos = await ContractLineQuery.BuildAsync(_db, _db.StudentContractLines.AsNoTracking().Where(l => l.Id == line.Id), ct);
        return dtos[0];
    }
}

// ---- Line cancel (güvenli; bağlı kayıt varsa 409) ----
public sealed record CancelContractLineCommand(Guid LineId) : ICommand<StudentContractLineDto>, IStudioWriteRequest;

public sealed class CancelContractLineHandler : ICommandHandler<CancelContractLineCommand, StudentContractLineDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public CancelContractLineHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<StudentContractLineDto> Handle(CancelContractLineCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var line = await _db.StudentContractLines.FirstOrDefaultAsync(l => l.Id == request.LineId && l.StudioId == studioId, ct)
            ?? throw new NotFoundException("Sözleşme satırı", request.LineId);

        if (line.Status == StudentContractLineStatus.Cancelled)
        {
            var same = await ContractLineQuery.BuildAsync(_db, _db.StudentContractLines.AsNoTracking().Where(l => l.Id == line.Id), ct);
            return same[0];
        }

        // Güvenli iptal: bağlı ödeme/ders/telafi varsa engelle (yanlış/boş açılmış line iptal edilebilir).
        if (await _db.Payments.AnyAsync(p => p.StudentContractLineId == line.Id && p.Status == PaymentStatus.Paid, ct))
            throw new ConflictException("Bu satırda ödenmiş ödeme var; iptal edilemez.");
        if (await _db.ClassBookings.AnyAsync(b => b.StudentContractLineId == line.Id && b.Status == ClassBookingStatus.Booked, ct))
            throw new ConflictException("Bu satıra bağlı planlı ders var; iptal edilemez.");
        if (await _db.MakeupCreditTransactions.AnyAsync(t => t.StudentContractLineId == line.Id, ct))
            throw new ConflictException("Bu satıra bağlı telafi kredisi hareketi var; iptal edilemez.");
        if (await _db.MakeupRequests.AnyAsync(r => r.StudentContractLineId == line.Id
            && (r.Status == MakeupRequestStatus.Pending || r.Status == MakeupRequestStatus.Approved), ct))
            throw new ConflictException("Bu satıra bağlı telafi talebi var; iptal edilemez.");

        line.Status = StudentContractLineStatus.Cancelled;
        await _db.SaveChangesAsync(ct);

        var dtos = await ContractLineQuery.BuildAsync(_db, _db.StudentContractLines.AsNoTracking().Where(l => l.Id == line.Id), ct);
        return dtos[0];
    }
}
