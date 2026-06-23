namespace Pliaforma.Application.Common.Interfaces;

/// <summary>
/// Sprint 32B: Ekipman kapasitesi tek satırda tutulmaz (used = çakışan seansların Capacity toplamı), bu yüzden
/// eşzamanlı seans oluşturma/güncellemeyi serileştirmek için Equipment havuz satırı üzerinde transaction-ömürlü
/// bir kilit alınır. Somut uygulama (PostgreSQL <c>SELECT ... FOR UPDATE</c>) Persistence'tadır; Application
/// sağlayıcıya bağlanmaz. Aktif transaction içinde çağrılmalı (TransactionBehavior); kilit commit'e kadar tutulur,
/// böylece aynı havuz için iki eşzamanlı rezervasyon "son kapasiteyi" iki kez veremez.
/// </summary>
public interface IEquipmentPoolLock
{
    Task LockEquipmentAsync(Guid studioId, Guid equipmentId, CancellationToken ct = default);
}
