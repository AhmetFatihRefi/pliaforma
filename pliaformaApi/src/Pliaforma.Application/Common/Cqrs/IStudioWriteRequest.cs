namespace Pliaforma.Application.Common.Cqrs;

/// <summary>
/// Bir stüdyo bağlamında YAZMA yapan komutları işaretler. Bu işaretçiyi taşıyan istekler:
/// - StudioStatusBehavior: stüdyo askıya alınmışsa reddeder.
/// - MembershipGuardBehavior: kullanıcının üyeliği hâlâ Active mi DB'den doğrular.
/// (Sprint 0'da auth komutları tenant-öncesi olduğu için bunu taşımaz; altyapı sonraki
/// sprintlerdeki stüdyo-içi komutlar için hazırdır.)
/// </summary>
public interface IStudioWriteRequest { }
