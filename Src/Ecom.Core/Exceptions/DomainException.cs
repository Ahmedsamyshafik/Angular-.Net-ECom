namespace Ecom.Core.Exceptions;

/// <summary>
/// Base type for all meaningful application/business failures.
/// Deliberately HTTP-free: deciding the HTTP status is the API layer's responsibility.
/// The global handler maps each derived exception explicitly (never this base type),
/// so every failure keeps its precise HTTP semantics.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}