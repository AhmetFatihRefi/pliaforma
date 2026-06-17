using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Packages;

public sealed record StudentPackageDto(
    Guid Id, Guid StudentProfileId, string StudentName, Guid? PackagePlanId, string Name,
    int InitialCreditCount, int CurrentCreditBalance, decimal Price,
    DateOnly ValidFrom, DateOnly? ValidUntil, StudentPackageStatus Status, StudentPackageStatus EffectiveStatus, DateTime CreatedAt);

public sealed record StudentPackageTransactionDto(
    Guid Id, int Amount, StudentPackageTransactionType Type, StudentPackageTransactionReason Reason,
    Guid? SourceAttendanceId, Guid? SourceClassBookingId, string? Notes, DateTime CreatedAt);

public sealed record StudentPackageDetailDto(StudentPackageDto Package, IReadOnlyList<StudentPackageTransactionDto> Transactions);

/// <summary>Paket bakiyesi + durum geçişlerini senkron tutan ortak kurallar.</summary>
internal static class PackageRules
{
    /// <summary>
    /// Görünen (effective) durum: DB Status Active ama ValidUntil geçmişse <c>Expired</c> gösterilir
    /// (otomatik job yok; DB Status değişmeyebilir — bkz. packages-and-payments.md). Diğer durumlar DB ile aynı.
    /// </summary>
    public static StudentPackageStatus Effective(StudentPackageStatus status, DateOnly? validUntil, DateOnly today) =>
        status == StudentPackageStatus.Active && validUntil != null && validUntil < today
            ? StudentPackageStatus.Expired
            : status;

    public static IQueryable<StudentPackageDto> Project(IQueryable<StudentPackage> q, DateOnly today) =>
        q.Select(p => new StudentPackageDto(
            p.Id, p.StudentProfileId, p.StudentProfile!.FullName, p.PackagePlanId, p.Name,
            p.InitialCreditCount, p.CurrentCreditBalance, p.Price, p.ValidFrom, p.ValidUntil, p.Status,
            p.Status == StudentPackageStatus.Active && p.ValidUntil != null && p.ValidUntil < today
                ? StudentPackageStatus.Expired
                : p.Status,
            p.CreatedAt));

    public static void ApplyBalance(StudentPackage pkg, int newBalance)
    {
        pkg.CurrentCreditBalance = newBalance;
        if (pkg.Status == StudentPackageStatus.Cancelled) return;
        pkg.Status = newBalance <= 0 ? StudentPackageStatus.UsedUp : StudentPackageStatus.Active;
    }
}

// ---- Öğrencinin paketleri ----
public sealed record GetStudentPackagesQuery(Guid StudentProfileId) : IQuery<IReadOnlyList<StudentPackageDto>>;

public sealed class GetStudentPackagesHandler : IQueryHandler<GetStudentPackagesQuery, IReadOnlyList<StudentPackageDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public GetStudentPackagesHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock) { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<IReadOnlyList<StudentPackageDto>> Handle(GetStudentPackagesQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));
        return await PackageRules.Project(
            _db.StudentPackages.Where(p => p.StudioId == studioId && p.StudentProfileId == request.StudentProfileId)
                .OrderByDescending(p => p.CreatedAt), today).ToListAsync(ct);
    }
}

// ---- Tek paket + hareketleri ----
public sealed record GetStudentPackageQuery(Guid Id) : IQuery<StudentPackageDetailDto>;

public sealed class GetStudentPackageHandler : IQueryHandler<GetStudentPackageQuery, StudentPackageDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public GetStudentPackageHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock) { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<StudentPackageDetailDto> Handle(GetStudentPackageQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));
        var pkg = await PackageRules.Project(_db.StudentPackages.Where(p => p.Id == request.Id && p.StudioId == studioId), today)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Öğrenci paketi", request.Id);

        var txs = await _db.StudentPackageTransactions
            .Where(t => t.StudioId == studioId && t.StudentPackageId == request.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new StudentPackageTransactionDto(
                t.Id, t.Amount, t.Type, t.Reason, t.SourceAttendanceId, t.SourceClassBookingId, t.Notes, t.CreatedAt))
            .ToListAsync(ct);

        return new StudentPackageDetailDto(pkg, txs);
    }
}

// ---- Paket sat / tanımla ----
public sealed record SellStudentPackageCommand(
    Guid StudentProfileId, Guid? PackagePlanId, string? Name, int? CreditCount, decimal? Price, int? ValidityDays)
    : ICommand<StudentPackageDto>, IStudioWriteRequest;

public sealed class SellStudentPackageValidator : AbstractValidator<SellStudentPackageCommand>
{
    public SellStudentPackageValidator()
    {
        RuleFor(x => x.StudentProfileId).NotEmpty();
        RuleFor(x => x.CreditCount).GreaterThan(0).When(x => x.CreditCount.HasValue);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).When(x => x.Price.HasValue);
        RuleFor(x => x.ValidityDays).GreaterThan(0).When(x => x.ValidityDays.HasValue);
        RuleFor(x => x.Name).MaximumLength(150);
    }
}

public sealed class SellStudentPackageHandler : ICommandHandler<SellStudentPackageCommand, StudentPackageDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public SellStudentPackageHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<StudentPackageDto> Handle(SellStudentPackageCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.StudentProfileId && s.StudioId == studioId, ct)
            ?? throw new NotFoundException("Öğrenci", request.StudentProfileId);
        if (student.Status == StudentStatus.Inactive)
            throw new BusinessRuleException("Pasif öğrenciye yeni paket tanımlanamaz.");

        string name;
        int credits;
        decimal price;
        int? validityDays;

        if (request.PackagePlanId is { } planId)
        {
            var plan = await _db.PackagePlans.FirstOrDefaultAsync(p => p.Id == planId && p.StudioId == studioId, ct)
                ?? throw new NotFoundException("Paket planı", planId);
            name = (request.Name ?? plan.Name).Trim();
            credits = request.CreditCount ?? plan.CreditCount;
            price = request.Price ?? plan.Price;
            validityDays = request.ValidityDays ?? plan.ValidityDays;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.CreditCount is null)
                throw new BusinessRuleException("Plan seçilmediyse paket adı ve kredi sayısı zorunludur.");
            name = request.Name.Trim();
            credits = request.CreditCount.Value;
            price = request.Price ?? 0m;
            validityDays = request.ValidityDays;
        }

        if (credits <= 0)
            throw new BusinessRuleException("Kredi sayısı 0'dan büyük olmalı.");

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, ct);
        var validFrom = DateOnly.FromDateTime(nowLocal);
        var validUntil = validityDays.HasValue ? validFrom.AddDays(validityDays.Value) : (DateOnly?)null;

        var pkg = new StudentPackage
        {
            StudioId = studioId,
            StudentProfileId = student.Id,
            PackagePlanId = request.PackagePlanId,
            Name = name,
            InitialCreditCount = credits,
            CurrentCreditBalance = credits,
            Price = price,
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            Status = StudentPackageStatus.Active,
            CreatedByUserId = userId,
        };
        _db.StudentPackages.Add(pkg);
        _db.StudentPackageTransactions.Add(new StudentPackageTransaction
        {
            StudioId = studioId,
            StudentPackageId = pkg.Id,
            StudentProfileId = student.Id,
            Amount = credits,
            Type = StudentPackageTransactionType.Granted,
            Reason = StudentPackageTransactionReason.PackagePurchased,
            CreatedByUserId = userId,
        });

        await _db.SaveChangesAsync(ct);
        var eff = PackageRules.Effective(pkg.Status, pkg.ValidUntil, validFrom);
        return new StudentPackageDto(pkg.Id, student.Id, student.FullName, pkg.PackagePlanId, pkg.Name,
            pkg.InitialCreditCount, pkg.CurrentCreditBalance, pkg.Price, pkg.ValidFrom, pkg.ValidUntil, pkg.Status, eff, pkg.CreatedAt);
    }
}

// ---- Paket iptal ----
public sealed record CancelStudentPackageCommand(Guid Id) : ICommand<Unit>, IStudioWriteRequest;

public sealed class CancelStudentPackageHandler : ICommandHandler<CancelStudentPackageCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public CancelStudentPackageHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<Unit> Handle(CancelStudentPackageCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var pkg = await _db.StudentPackages.FirstOrDefaultAsync(p => p.Id == request.Id && p.StudioId == studioId, ct)
            ?? throw new NotFoundException("Öğrenci paketi", request.Id);
        if (pkg.Status == StudentPackageStatus.Cancelled)
            throw new ConflictException("Paket zaten iptal edilmiş.");

        pkg.Status = StudentPackageStatus.Cancelled; // bakiye korunur; iptal paket tüketilemez (atomik UPDATE Status='Active' arar)
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ---- Manuel düzeltme (adjust) ----
public sealed record AdjustStudentPackageCommand(Guid Id, int Amount, string? Notes) : ICommand<StudentPackageDto>, IStudioWriteRequest;

public sealed class AdjustStudentPackageValidator : AbstractValidator<AdjustStudentPackageCommand>
{
    public AdjustStudentPackageValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Amount).NotEqual(0).WithMessage("Düzeltme miktarı 0 olamaz.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class AdjustStudentPackageHandler : ICommandHandler<AdjustStudentPackageCommand, StudentPackageDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public AdjustStudentPackageHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock) { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<StudentPackageDto> Handle(AdjustStudentPackageCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var pkg = await _db.StudentPackages.FirstOrDefaultAsync(p => p.Id == request.Id && p.StudioId == studioId, ct)
            ?? throw new NotFoundException("Öğrenci paketi", request.Id);
        if (pkg.Status == StudentPackageStatus.Cancelled)
            throw new ConflictException("İptal edilmiş paket düzeltilemez.");

        var newBalance = pkg.CurrentCreditBalance + request.Amount;
        if (newBalance < 0)
            throw new BusinessRuleException($"Bakiye negatif olamaz (mevcut {pkg.CurrentCreditBalance}).");

        PackageRules.ApplyBalance(pkg, newBalance);
        _db.StudentPackageTransactions.Add(new StudentPackageTransaction
        {
            StudioId = studioId,
            StudentPackageId = pkg.Id,
            StudentProfileId = pkg.StudentProfileId,
            Amount = request.Amount,
            Type = StudentPackageTransactionType.Adjusted,
            Reason = StudentPackageTransactionReason.ManualAdjustment,
            CreatedByUserId = userId,
            Notes = request.Notes?.Trim(),
        });

        await _db.SaveChangesAsync(ct);
        var name = await _db.StudentProfiles.Where(s => s.Id == pkg.StudentProfileId).Select(s => s.FullName).FirstAsync(ct);
        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));
        var eff = PackageRules.Effective(pkg.Status, pkg.ValidUntil, today);
        return new StudentPackageDto(pkg.Id, pkg.StudentProfileId, name, pkg.PackagePlanId, pkg.Name,
            pkg.InitialCreditCount, pkg.CurrentCreditBalance, pkg.Price, pkg.ValidFrom, pkg.ValidUntil, pkg.Status, eff, pkg.CreatedAt);
    }
}
