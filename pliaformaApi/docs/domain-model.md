# Domain Modeli

Tüm entity'ler `BaseEntity`'den türer: `Id (Guid, istemci üretir)`, `CreatedAt`, `UpdatedAt?`,
`IsDeleted` (soft-delete, global query filter ile gizlenir; `AuditInterceptor` doldurur).
`ITenantScoped` uygulayan entity'ler `StudioId` taşır ve stüdyo bazında izole edilir.

## Sprint 0 entity'leri

### User  *(tenant-scoped DEĞİL — global kimlik)*
| Alan | Tip | Not |
|---|---|---|
| Email / NormalizedEmail | string | NormalizedEmail **global benzersiz** (unique index) |
| PasswordHash | string | BCrypt |
| FullName, Phone? | string | |
| GlobalRole | enum | `Standard \| PlatformAdmin` |
| IsActive | bool | devre dışı kullanıcı giriş/refresh yapamaz |
| LastLoginAt? | DateTime | |

### Studio  *(tenant kökü)*
| Alan | Tip | Not |
|---|---|---|
| Name, Slug | string | Slug **benzersiz** (URL/alt alan adı) |
| TimeZoneId | string | IANA (örn. Europe/Istanbul) — tekrarlama/hatırlatma için |
| Currency | string(3) | varsayılan TRY |
| ContactEmail/Phone, AddressLine, City | string? | |
| Status | enum | `Active \| Suspended \| Cancelled` (Sprint 10). Yalnızca Active iken yazılır (StudioStatusBehavior); Suspended okur ama yazamaz, veri silinmez. `IsActive` = hesaplanan `Status==Active` (kolon değil) |
| SuspendedAt?, SuspendedReason?, ReactivatedAt?, UpdatedByPlatformAdminUserId? | — | platform admin askı/aktifleştirme denetim alanları (Sprint 10) |
| OwnerUserId → User | FK | tek sahip hesabı |

### StudioMembership  *(ITenantScoped)*
| Alan | Tip | Not |
|---|---|---|
| StudioId → Studio | FK | |
| UserId → User | FK | (StudioId, UserId) **benzersiz** — kullanıcı stüdyoda tek üyelik |
| Role | enum | `Owner \| StudioAdmin \| Instructor \| Student` |
| Status | enum | `Pending \| Active \| Rejected \| Removed` |
| Direction | enum | `InvitedByStudio \| RequestedByUser` |
| AcceptedAt? | DateTime | |

### RefreshToken  *(User'a bağlı)*
| Alan | Tip | Not |
|---|---|---|
| UserId → User | FK | |
| TokenHash | string | ham token DEĞİL, SHA-256 hash; **benzersiz** |
| ExpiresAt, RevokedAt? | DateTime | rotasyonlu |
| ReplacedByTokenHash? | string | |
| CreatedByIp? | string | |

## ER (Sprint 0)

```
User 1───* StudioMembership *───1 Studio
User 1───* RefreshToken
Studio *───1 User (Owner)
```

## Sprint 1 entity'leri (eklendi)

- **InstructorProfile** *(TS)* — `(StudioId,UserId)` benzersiz. DisplayName, Bio?, **Rate**, RateType,
  Color?, IsAcceptingStudents. Hoca ücreti **stüdyo bazında**.
- **ClassType** *(TS)* — Equipment × CapacityType × Level × Purpose + DefaultDurationMinutes (+Name,
  Description?, IsActive, MaxCapacity). "Pilates grubu". Kapasite kuralı: Private⇒1, Duo⇒2, Group⇒2..50.
- **InstructorClassType** *(TS)* — hoca↔grup, `(InstructorProfileId,ClassTypeId)` benzersiz, RateOverride?.

Enum kataloğu: `GlobalRole`, `StudioRole`, `MembershipStatus`, `MembershipDirection` (Sprint 0) +
`EquipmentType`, `ClassCapacityType`, `ClassLevel`, `ClassPurpose`, `RateType` (Sprint 1). DB'de **string**.
Detay: [instructors-and-classtypes.md](instructors-and-classtypes.md).

## Sprint 2 entity'si (eklendi)

- **StudentProfile** *(TS)* — `UserId?` (null=misafir), `StudioMembershipId?`, FullName(zorunlu),
  Phone?, Email?, `Status: Guest|Active|Inactive`, `Source: Phone|WalkIn|Web|Referral|Other`, Notes?,
  **HealthNotes?** (hassas), EmergencyContact(Name/Phone)?, BirthDate?, **CreatedByUserId** (hoca
  scope), ConvertedAt?, IsActive. İndeksler: (StudioId, CreatedByUserId), (StudioId, UserId).
  Misafir→User dönüşümünde **Id korunur** (silinmez); ileride rezervasyon/ödeme/yoklama bu Id'ye bağlanır.
  Enumlar: `StudentStatus`, `StudentSource`. Detay: [students-and-guest-flow.md](students-and-guest-flow.md).

---

## Sprint 3 entity'leri (eklendi)

- **InstructorAvailability** *(TS)* — InstructorProfileId, `DayOfWeek` (System.DayOfWeek, DB'de string),
  `StartTime`/`EndTime` (TimeOnly, stüdyo-yerel), IsActive. Haftalık tekrar eden müsaitlik penceresi.
- **ClassSession** *(TS)* — InstructorProfileId, ClassTypeId, `StartAt`/`EndAt` (`timestamp without time
  zone`, stüdyo-yerel duvar saati), `Capacity`, `Status` (`ClassSessionStatus: Scheduled|Cancelled|
  Completed`), Notes?, CreatedByUserId. **EndAt = StartAt + ClassType.DefaultDurationMinutes**; Capacity
  vars. `ClassType.MaxCapacity`. Çakışma/müsaitlik/geçmiş kuralları. Sprint 4 rezervasyonunun bağlanacağı
  birim. Detay: [scheduling-and-availability.md](scheduling-and-availability.md).

---

## Sprint 4 entity'si (eklendi)

- **ClassBooking** *(TS)* — ClassSessionId, **StudentProfileId** (UserId değil), `Status`
  (`ClassBookingStatus: Booked|CancelledByStudent|CancelledByStudio`), BookedByUserId, CancelledByUserId?,
  CancelledAt?, CancellationReason?. **Filtered unique index** `(ClassSessionId, StudentProfileId)
  WHERE Status='Booked' AND IsDeleted=false` (çift-rezervasyon). `ClassSession.BookedCount` alanı eklendi;
  kapasite atomik UPDATE ile tüketilir (race-safe). Detay: [bookings-and-capacity.md](bookings-and-capacity.md).

---

## Sprint 5 entity'leri (eklendi)

- **ClassSession** *(güncellendi)* — `SessionKind` (`ClassSessionKind: Regular|Makeup|Trial|Special`,
  DB'de string, vars. Regular) + erteleme izi `RescheduledAt?`/`RescheduleReason?`. Makeup seansı
  rezervasyonu 1 telafi kredisi tüketir.
- **MakeupCreditTransaction** *(TS)* — StudentProfileId, `Amount` (işaretli int, 0 olamaz), `Type`
  (`Granted|Used|Restored|Revoked|Expired`), `Reason` (`StudioCancelledClass|HolidayClosure|ManualByOwner|
  ManualByInstructor|MakeupBookingUsed|MakeupBookingCancelled|StudentCancelledInTime|NoShowApproved|Other`),
  SourceClassSessionId?, SourceBookingId?, UsedForBookingId?, ExpiresAt?, CreatedByUserId, Notes?. **Bakiye =
  SUM(Amount)**; soft delete edilmez (ters hareket eklenir). Filtered unique index'ler:
  `(StudentProfileId, SourceClassSessionId) WHERE Type='Granted'` (seans başına tek otomatik kredi) ve
  `(UsedForBookingId) WHERE NOT NULL` (booking başına tek tüketim).
- **StudioClosure** *(TS)* — Title, Reason?, StartAt/EndAt (`timestamp without time zone`), `ActionType`
  (`StudioClosureActionType: CancelSessions|CancelAndGrantMakeupCredit|RescheduleByOffset`), OffsetDays?,
  AffectedInstructorProfileId?, AffectedClassTypeId?, sonuç sayıları (affected/cancelled/credited/
  rescheduled/skipped), CreatedByUserId. Tek seferlik toplu operasyon (recurring DEĞİL).
- **StudioMakeupPolicy** *(TS)* — `(StudioId)` benzersiz; MonthlyManualCreditLimit (vars. 2),
  DefaultCreditValidityDays (vars. 30), AllowInstructorManualGrant (vars. true),
  AllowMakeupBookingWithoutCreditByAdmin (vars. false). Kayıt yoksa varsayılan; ilk güncellemede oluşur.

Detay: [makeup-credits-and-postponements.md](makeup-credits-and-postponements.md).

---

## Sprint 6 entity'si (eklendi)

- **Attendance** *(TS)* — **ClassBookingId** (booking üzerinden; filtered unique index `(ClassBookingId)
  WHERE IsDeleted=false` → booking başına tek kayıt), `ClassSessionId`/`StudentProfileId` (booking ile
  tutarlı denormalize), `Status` (`AttendanceStatus: Present|Absent|Excused|LateCancel`, DB'de string),
  `MarkedByUserId`, `MarkedAt` (UTC, denetim), Notes?, **MakeupCreditTransactionId?** (bu yoklamadan doğan
  kredi — ikinci kez kredi vermeyi engeller). İndeksler: (StudioId, ClassSessionId), (StudioId,
  StudentProfileId). Yoklama ödeme/paket hakkı DÜŞÜRMEZ. Enum: `MakeupCreditReason`'a `ExcusedAbsence` (9)
  eklendi (Excused→ExcusedAbsence, LateCancel→NoShowApproved kredi gerekçesi). Detay: [attendance.md](attendance.md).

---

## Sprint 7 entity'leri (eklendi)

- **PackagePlan** *(TS)* — paket şablonu: Name (`(StudioId,Name)` benzersiz), Description?, CreditCount(>0),
  Price(decimal), ValidityDays?, IsActive. Bu sürümde class type kısıtı yok.
- **StudentPackage** *(TS)* — öğrenciye satılan paket: StudentProfileId, PackagePlanId?, Name,
  InitialCreditCount, **CurrentCreditBalance** (ledger ile senkron, negatif olamaz), Price, ValidFrom
  (DateOnly), ValidUntil? (DateOnly), `Status` (`StudentPackageStatus: Active|UsedUp|Expired|Cancelled`),
  CreatedByUserId. Silinmez; Cancelled olabilir. Misafire satılabilir; pasif öğrenciye yeni paket yok.
- **StudentPackageTransaction** *(TS)* — paket hakkı defteri: StudentPackageId, StudentProfileId, Amount
  (işaretli, 0 olamaz), `Type` (`Granted|Used|Restored|Adjusted|Revoked`), `Reason` (`PackagePurchased|
  AttendancePresent|AttendanceAbsent|AttendanceLateCancel|ManualAdjustment|AdminCorrection|BookingCancelled|
  Other`), SourceAttendanceId?, SourceClassBookingId?, CreatedByUserId, Notes?. Soft delete edilmez (ters
  hareket). Filtered unique index `(SourceAttendanceId) WHERE Type='Used'` → yoklama başına tek düşüm.
- **Payment** *(TS)* — manuel ödeme: StudentProfileId, StudentPackageId?, Amount(decimal), Currency,
  DueDate (DateOnly), PaidAt?, `Status` (`PaymentStatus: Pending|Paid|Overdue|Cancelled|Refunded` — **Overdue
  dinamik**), `Method?` (`Cash|Card|BankTransfer|Other`), Notes?, CreatedByUserId. **Package'tan ayrı** (ödeme
  hak düşürmez). Para alanları `numeric(12,2)`.

Detay: [packages-and-payments.md](packages-and-payments.md).

---

## Sprint 8 (yeni entity YOK)

Dashboard ve raporlar **read-only**dir; yeni tablo/şema eklemez (migration yok). Mevcut entity'ler üzerinden
`AsNoTracking` + projection ile aylık agregasyon yapar. Detay: [dashboards-and-reports.md](dashboards-and-reports.md).

## Sprint 9 entity'si (eklendi)

- **Notification** *(TS + kullanıcı-scoped)* — RecipientUserId (zorunlu), `Type`
  (`NotificationType: ClassReminder|PaymentDueSoon|PaymentOverdue|MakeupCreditGranted|BookingCancelled|
  SessionRescheduled|System`), `Channel` (`NotificationChannel: InApp|Log|Email|Sms|WhatsApp` — bu sürümde
  yalnız InApp/Log aktif), `Status` (`NotificationStatus: Pending|Sent|Failed|Read`), Title, Body,
  **DeduplicationKey** (`(StudioId, DeduplicationKey)` filtered unique → olay başına tek bildirim),
  RelatedClassSessionId?/RelatedClassBookingId?/RelatedPaymentId?, ScheduledAtUtc?/SentAtUtc?/ReadAtUtc?,
  FailureReason?. Detay: [notifications-and-reminders.md](notifications-and-reminders.md).

## Sprint 10 entity'si (eklendi)

- **PlatformSubscription** — bir stüdyonun platform aboneliği (**basit takip iskeleti; gerçek tahsilat YOK**).
  StudioId (`(StudioId)` filtered unique → stüdyo başına bir abonelik), PlanName, Tier?, `Status`
  (`SubscriptionStatus: Trialing|Active|PastDue|Suspended|Cancelled`), MonthlyPrice(decimal), Currency,
  `BillingPeriod` (`Monthly|Yearly`), CurrentPeriodStart/End (DateOnly), TrialEndsAt?(DateOnly),
  CancelledAt?, Notes?. Yalnızca **PlatformAdmin** yönetir (`/api/platform/*`). `StudioStatus` ile birebir aynı
  olmak zorunda değildir. Ayrıca `Studio` entity'sine `Status` + askı denetim alanları eklendi (yukarıda).
  Yeni indeks: **`Payments (StudioId, DueDate)`** (index incelemesi). Detay:
  [platform-admin-and-observability.md](platform-admin-and-observability.md).

Enum kataloğuna eklendi: `StudioStatus`, `SubscriptionStatus`, `BillingPeriod` (Sprint 10).

---

## İleri sprint şeması (henüz YAPILMADI — referans)

- **RecurringBookingPattern**, gerçek bildirim kanalları (SMS/WhatsApp/e-posta), online ödeme/abonelik
  tahsilatı gateway, gelişmiş gözlemlenebilirlik/metrik. [Sprint 11+]

Karar gerekçeleri: [adr/](adr/).
