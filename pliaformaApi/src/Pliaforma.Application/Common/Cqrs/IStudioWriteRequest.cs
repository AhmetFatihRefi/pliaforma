namespace Pliaforma.Application.Common.Cqrs;

/// <summary>
/// Bir stüdyo bağlamında YAZMA yapan komutları işaretler. Bu işaretçiyi taşıyan istekler:
/// - StudioStatusBehavior: stüdyo askıya alınmışsa reddeder.
/// - MembershipGuardBehavior: kullanıcının üyeliği hâlâ Active mi + token rolü DB rolüyle uyumlu mu doğrular.
/// (Sprint 0'da auth komutları tenant-öncesi olduğu için bunu taşımaz; altyapı sonraki
/// sprintlerdeki stüdyo-içi komutlar için hazırdır.)
/// </summary>
public interface IStudioWriteRequest { }

/// <summary>
/// Sprint 48: HASSAS PII/finans OKUMA sorgularını işaretler (başka kişilerin öğrenci/sağlık/sözleşme/ödeme verisi).
/// Yalnız <see cref="MembershipGuardBehavior{TRequest,TResponse}"/> için: stüdyodan çıkarılan (Removed) ya da rolü
/// düşürülen bir üye, access token'ı süresi dolmadan da bu yüzeyleri OKUYAMAZ (write guard'ıyla simetrik).
/// StudioStatus/SubscriptionLicense guard'larını TETİKLEMEZ (onlar yalnız IStudioWriteRequest'e bakar) → okuma
/// yolları askı/lisans kapısından etkilenmez, davranış değişmez.
/// </summary>
public interface IStudioReadRequest { }
