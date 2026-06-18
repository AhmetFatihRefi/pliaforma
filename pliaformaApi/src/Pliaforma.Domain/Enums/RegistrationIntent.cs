namespace Pliaforma.Domain.Enums;

/// <summary>
/// Kullanıcının kayıt sırasındaki niyeti (öğrenci mi hoca mı katılacak). Sprint 17 Rev2.
/// **Yalnızca UX amaçlıdır** — stüdyosuz kullanıcının /join-studio ekranında hangi akışı göreceğini
/// belirler. Bir yetkilendirme/rol kaynağı DEĞİLDİR (gerçek rol StudioMembership.Role'dür).
/// Eski kullanıcılarda <see cref="Unknown"/> olabilir → nötr seçim ekranı gösterilir.
/// </summary>
public enum RegistrationIntent
{
    Unknown = 0,
    Student = 1,
    Instructor = 2,
}
