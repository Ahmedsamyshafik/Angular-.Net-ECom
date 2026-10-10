namespace Ecom.Core.Exceptions;

/// <summary>
/// Thrown when a change cannot be applied because it conflicts with the current
/// state of the resource. Always maps to HTTP 409 Conflict.
/// </summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }
}