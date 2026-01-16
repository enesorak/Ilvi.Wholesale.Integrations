namespace Ilvi.Core.Abstractions;

public interface IEntity
{
    DateTime CreatedAtUtc { get; }
    DateTime? UpdatedAtUtc { get; }
    string ComputedHash { get; }
}