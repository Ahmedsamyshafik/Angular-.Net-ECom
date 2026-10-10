namespace Ecom.Core.Exceptions;

/// <summary>
/// Thrown when a request cannot be processed because its input violates a
/// business rule (e.g. required data is missing or inconsistent).
/// Always maps to HTTP 400 Bad Request.
/// </summary>
public sealed class BadRequestException : DomainException
{
    public BadRequestException(string message) : base(message)
    {
    }
}