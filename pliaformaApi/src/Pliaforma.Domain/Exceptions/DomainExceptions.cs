namespace Pliaforma.Domain.Exceptions;

/// <summary>Uygulamaya özgü, kullanıcıya gösterilebilir hataların kök tipi.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

/// <summary>Aranan kayıt bulunamadı (HTTP 404).</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string entity, object key)
        : base($"{entity} bulunamadı (anahtar: {key}).") { }

    public NotFoundException(string message) : base(message) { }
}

/// <summary>İş kuralı ihlali (HTTP 409).</summary>
public sealed class BusinessRuleException : AppException
{
    public BusinessRuleException(string message) : base(message) { }
}

/// <summary>Yetkisiz işlem (HTTP 403).</summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message) { }
}

/// <summary>Kimlik doğrulama hatası — geçersiz kimlik bilgisi / token (HTTP 401).</summary>
public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message) : base(message) { }
}

/// <summary>Çakışma — örn. e-posta zaten kayıtlı (HTTP 409).</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>Kullanımdan kaldırılmış/artık desteklenmeyen akış (HTTP 410 Gone). Örn. JoinCode self-apply (Rev3 invite-only).</summary>
public sealed class GoneException : AppException
{
    public GoneException(string message) : base(message) { }
}
