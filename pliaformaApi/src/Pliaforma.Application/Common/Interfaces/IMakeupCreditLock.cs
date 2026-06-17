namespace Pliaforma.Application.Common.Interfaces;

/// <summary>
/// Telafi kredisi bakiyesi tek bir satırda tutulmaz (ledger toplamıdır), bu yüzden eşzamanlı
/// kredi tüketimini serileştirmek için öğrenci satırı üzerinde transaction-ömürlü bir kilit alınır.
/// Somut uygulama (PostgreSQL <c>SELECT ... FOR UPDATE</c>) Persistence'tadır; Application sağlayıcıya
/// bağlanmaz. Aktif transaction içinde çağrılmalıdır (TransactionBehavior); kilit commit'e kadar tutulur,
/// böylece aynı öğrenci için iki eşzamanlı telafi rezervasyonu "son krediyi" iki kez harcayamaz.
/// </summary>
public interface IMakeupCreditLock
{
    Task LockStudentAsync(Guid studioId, Guid studentProfileId, CancellationToken ct = default);
}
