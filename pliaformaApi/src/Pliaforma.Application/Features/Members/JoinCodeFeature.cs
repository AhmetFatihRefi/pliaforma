using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Text;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Members;

/// <summary>Stüdyo katılım kodu görünümü (Owner/StudioAdmin). Formatlı (ABCD-EFGH-JK) + durum.</summary>
public sealed record JoinCodeDto(string JoinCode, string FormattedJoinCode, bool IsActive, DateTime? UpdatedAt);

// ---- Kodu getir ----
public sealed record GetJoinCodeQuery : IQuery<JoinCodeDto>;

public sealed class GetJoinCodeHandler : IQueryHandler<GetJoinCodeQuery, JoinCodeDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetJoinCodeHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<JoinCodeDto> Handle(GetJoinCodeQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var studio = await _db.Studios.FirstOrDefaultAsync(s => s.Id == studioId, ct)
            ?? throw new NotFoundException("Stüdyo", studioId);
        var code = studio.JoinCode ?? string.Empty;
        return new JoinCodeDto(code, JoinCode.Format(code), studio.JoinCodeIsActive, studio.JoinCodeUpdatedAt);
    }
}

// ---- Kodu yenile (eski kod geçersizleşir) ----
// Not: IStudioWriteRequest DEĞİL — ayarsal/idari aksiyon; lisans/suspend kapısına bağlanmaz.
public sealed record RegenerateJoinCodeCommand : ICommand<JoinCodeDto>;

public sealed class RegenerateJoinCodeHandler : ICommandHandler<RegenerateJoinCodeCommand, JoinCodeDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public RegenerateJoinCodeHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<JoinCodeDto> Handle(RegenerateJoinCodeCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var studio = await _db.Studios.FirstOrDefaultAsync(s => s.Id == studioId, ct)
            ?? throw new NotFoundException("Stüdyo", studioId);

        string code;
        do { code = JoinCode.Generate(); }
        while (await _db.Studios.AnyAsync(s => s.JoinCode == code && s.Id != studioId, ct));

        studio.JoinCode = code;
        studio.JoinCodeIsActive = true;
        studio.JoinCodeUpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new JoinCodeDto(code, JoinCode.Format(code), studio.JoinCodeIsActive, studio.JoinCodeUpdatedAt);
    }
}
