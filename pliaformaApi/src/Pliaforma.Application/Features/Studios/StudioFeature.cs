using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Studios;

public sealed record StudioDto(
    Guid Id,
    string Name,
    string Slug,
    string TimeZoneId,
    string Currency,
    string? ContactEmail,
    string? ContactPhone,
    string? AddressLine,
    string? City,
    bool IsActive)
{
    public static StudioDto From(Studio s) =>
        new(s.Id, s.Name, s.Slug, s.TimeZoneId, s.Currency, s.ContactEmail, s.ContactPhone, s.AddressLine, s.City, s.IsActive);
}

// ---- Aktif stüdyoyu getir ----
public sealed record GetMyStudioQuery : IQuery<StudioDto>;

public sealed class GetMyStudioHandler : IQueryHandler<GetMyStudioQuery, StudioDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyStudioHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StudioDto> Handle(GetMyStudioQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var studio = await _db.Studios.FirstOrDefaultAsync(s => s.Id == studioId, cancellationToken)
            ?? throw new NotFoundException("Stüdyo", studioId);
        return StudioDto.From(studio);
    }
}

// ---- Aktif stüdyo ayarlarını güncelle ----
public sealed record UpdateStudioCommand(
    string Name,
    string TimeZoneId,
    string Currency,
    string? ContactEmail,
    string? ContactPhone,
    string? AddressLine,
    string? City) : ICommand<StudioDto>, IStudioWriteRequest;

public sealed class UpdateStudioValidator : AbstractValidator<UpdateStudioCommand>
{
    public UpdateStudioValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.ContactEmail).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        RuleFor(x => x.ContactPhone).MaximumLength(32);
        RuleFor(x => x.AddressLine).MaximumLength(300);
        RuleFor(x => x.City).MaximumLength(100);
    }
}

public sealed class UpdateStudioHandler : ICommandHandler<UpdateStudioCommand, StudioDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateStudioHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StudioDto> Handle(UpdateStudioCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var studio = await _db.Studios.FirstOrDefaultAsync(s => s.Id == studioId, cancellationToken)
            ?? throw new NotFoundException("Stüdyo", studioId);

        studio.Name = request.Name.Trim();
        studio.TimeZoneId = request.TimeZoneId.Trim();
        studio.Currency = request.Currency.Trim().ToUpperInvariant();
        studio.ContactEmail = request.ContactEmail?.Trim();
        studio.ContactPhone = request.ContactPhone?.Trim();
        studio.AddressLine = request.AddressLine?.Trim();
        studio.City = request.City?.Trim();

        await _db.SaveChangesAsync(cancellationToken);
        return StudioDto.From(studio);
    }
}
