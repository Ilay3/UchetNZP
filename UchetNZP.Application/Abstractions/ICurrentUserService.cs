using System;

namespace UchetNZP.Application.Abstractions;

public interface ICurrentUserService
{
    Guid UserId { get; }
}
