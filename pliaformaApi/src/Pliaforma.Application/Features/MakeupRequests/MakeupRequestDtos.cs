using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.MakeupRequests;

/// <summary>
/// Telafi dersi talebi görünümü (Sprint 27). Pending talep gerçek ders DEĞİLDİR; bu yüzden attendance/confirm/booking
/// alanları yoktur. <see cref="HasConflict"/>/<see cref="ConflictReason"/> yalnız Pending kayıtlar için hesaplanır
/// (owner ekranı "çakışma durumu"). Approved talep <see cref="ResultingClassSessionId"/> ile gerçek seansa bağlanır.
/// </summary>
public sealed record MakeupRequestDto(
    Guid Id,
    Guid StudentProfileId,
    string StudentName,
    Guid RequestedInstructorProfileId,
    string InstructorName,
    Guid ClassTypeId,
    string ClassTypeName,
    DateTime StartAt,
    DateTime EndAt,
    MakeupRequestStatus Status,
    string? Description,
    Guid RequestedByUserId,
    Guid? DecisionByUserId,
    DateTime? DecidedAt,
    string? DecisionNote,
    Guid? ResultingClassSessionId,
    Guid? ResultingBookingId,
    DateTime CreatedAt,
    bool HasConflict,
    string? ConflictReason);

/// <summary>
/// MakeupRequest → DTO dönüştürücü. İsimler (öğrenci/hoca/ders tipi) tek toplu sorgularla (N+1 yok) çözülür;
/// Pending kayıtlar için çakışma durumu (gerçek hoca/öğrenci + diğer pending) hesaplanır.
/// </summary>
internal static class MakeupRequestView
{
    public static async Task<List<MakeupRequestDto>> BuildAsync(
        IAppDbContext db, Guid studioId, IReadOnlyList<MakeupRequest> requests, CancellationToken ct)
    {
        if (requests.Count == 0) return new();

        var studentIds = requests.Select(r => r.StudentProfileId).ToHashSet();
        var instructorIds = requests.Select(r => r.RequestedInstructorProfileId).ToHashSet();
        var classTypeIds = requests.Select(r => r.ClassTypeId).ToHashSet();

        var studentNames = await db.StudentProfiles
            .Where(s => s.StudioId == studioId && studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.FullName }).ToDictionaryAsync(s => s.Id, s => s.FullName, ct);
        var instructorNames = await db.InstructorProfiles
            .Where(p => p.StudioId == studioId && instructorIds.Contains(p.Id))
            .Select(p => new { p.Id, p.DisplayName }).ToDictionaryAsync(p => p.Id, p => p.DisplayName, ct);
        var classTypeNames = await db.ClassTypes
            .Where(c => c.StudioId == studioId && classTypeIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var dtos = new List<MakeupRequestDto>(requests.Count);
        foreach (var r in requests)
        {
            string? conflict = r.Status == MakeupRequestStatus.Pending
                ? await ComputeConflictAsync(db, studioId, r, ct)
                : null;

            dtos.Add(new MakeupRequestDto(
                r.Id, r.StudentProfileId, studentNames.TryGetValue(r.StudentProfileId, out var sn) ? sn : "",
                r.RequestedInstructorProfileId, instructorNames.TryGetValue(r.RequestedInstructorProfileId, out var inm) ? inm : "",
                r.ClassTypeId, classTypeNames.TryGetValue(r.ClassTypeId, out var cn) ? cn : "",
                r.StartAt, r.EndAt, r.Status, r.Description, r.RequestedByUserId, r.DecisionByUserId, r.DecidedAt,
                r.DecisionNote, r.ResultingClassSessionId, r.ResultingBookingId, r.CreatedAt,
                conflict is not null, conflict));
        }
        return dtos;
    }

    /// <summary>Pending talebin slotu şu an gerçek bir derse (hoca/öğrenci) veya başka bir pending talebe çakışıyor mu?</summary>
    private static async Task<string?> ComputeConflictAsync(IAppDbContext db, Guid studioId, MakeupRequest r, CancellationToken ct)
    {
        if (await SessionRules.HasConflictAsync(db, studioId, r.RequestedInstructorProfileId, r.StartAt, r.EndAt, null, ct))
            return "instructor";
        if (await SessionRules.StudentConflictAsync(db, studioId, new[] { r.StudentProfileId }, r.StartAt, r.EndAt, System.Array.Empty<Guid>(), ct))
            return "student";
        if (await SessionRules.PendingInstructorConflictAsync(db, studioId, r.RequestedInstructorProfileId, r.StartAt, r.EndAt, r.Id, ct))
            return "pending-instructor";
        if (await SessionRules.PendingStudentConflictAsync(db, studioId, new[] { r.StudentProfileId }, r.StartAt, r.EndAt, r.Id, ct))
            return "pending-student";
        return null;
    }
}
