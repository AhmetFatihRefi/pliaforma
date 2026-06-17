using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;

namespace Pliaforma.Application.Features.Notifications;

/// <summary>
/// Aktif stüdyo için hatırlatma taramasını ELLE çalıştırır (Owner/StudioAdmin). Worker'ın yaptığı işin
/// aynısını yalnızca current studio üzerinde yapar — smoke/test ve "şimdi çalıştır" için. Idempotent.
/// </summary>
public sealed record RunStudioRemindersCommand : ICommand<ReminderRunResultDto>, IStudioWriteRequest;

public sealed class RunStudioRemindersHandler : ICommandHandler<RunStudioRemindersCommand, ReminderRunResultDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ReminderScanner _scanner;

    public RunStudioRemindersHandler(ICurrentUserService currentUser, ReminderScanner scanner)
    {
        _currentUser = currentUser;
        _scanner = scanner;
    }

    public Task<ReminderRunResultDto> Handle(RunStudioRemindersCommand request, CancellationToken ct)
    {
        var sid = _currentUser.RequireStudioId();
        return _scanner.RunForStudioAsync(sid, ct);
    }
}
