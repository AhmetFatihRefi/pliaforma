using FluentValidation;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Members;

/// <summary>
/// **DEPRECATED (Sprint 17 Rev3 — invite-only onboarding).** JoinCode ile self-service stüdyoya katılım
/// artık YOKTUR — ne öğrenci ne hoca kodla başvurabilir. Stüdyoya katılım yalnızca stüdyonun **daveti**
/// (InviteMember → AcceptMembership) ile olur. Komut/endpoint geriye-uyum için durur ama **her zaman 410 Gone**
/// döner; hiçbir başvuru kaydı oluşturmaz. JoinCode üretim endpoint'leri DB'de kalır (UI'dan kaldırıldı).
/// </summary>
public sealed record ApplyToStudioCommand(string JoinCode, StudioRole Role) : ICommand<PendingInvitationDto>;

public sealed class ApplyToStudioValidator : AbstractValidator<ApplyToStudioCommand>
{
    // Rev3: kural yok — komut her durumda handler'da 410 ile kapanır (validator pre-reject etmez ki 410 tutarlı olsun).
    public ApplyToStudioValidator() { }
}

public sealed class ApplyToStudioHandler : ICommandHandler<ApplyToStudioCommand, PendingInvitationDto>
{
    // Invite-only: self-apply tamamen kapalı (öğrenci + hoca). 410 Gone + net mesaj.
    // NOT: faulted Task döndürülür (senkron `throw` reflection ile çağrıldığında TargetInvocationException'a
    // sarılır ve 500'e düşerdi); böylece gerçek GoneException middleware'e ulaşır → 410.
    public Task<PendingInvitationDto> Handle(ApplyToStudioCommand request, CancellationToken cancellationToken) =>
        Task.FromException<PendingInvitationDto>(new GoneException("Stüdyoya katılım yalnız davet ile yapılır."));
}
