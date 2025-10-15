using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UchetNZP.Application.Abstractions;

namespace UchetNZP.Web.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _cachedUserId;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public Guid UserId
    {
        get
        {
            if (_cachedUserId.HasValue)
            {
                return _cachedUserId.Value;
            }

            var principal = _httpContextAccessor.HttpContext?.User;
            if (principal is null)
            {
                _cachedUserId = Guid.Empty;
                return _cachedUserId.Value;
            }

            var identifier = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("sub")
                ?? principal.FindFirstValue("uid")
                ?? principal.Identity?.Name;

            if (!string.IsNullOrWhiteSpace(identifier) && Guid.TryParse(identifier, out var parsed))
            {
                _cachedUserId = parsed;
                return parsed;
            }

            _cachedUserId = Guid.Empty;
            return _cachedUserId.Value;
        }
    }
}
