namespace Ecom.Core.Exceptions;

/// <summary>
/// Thrown when a requested resource does not exist (or has been soft-deleted).
/// Always maps to HTTP 404 Not Found.
/// </summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }
}