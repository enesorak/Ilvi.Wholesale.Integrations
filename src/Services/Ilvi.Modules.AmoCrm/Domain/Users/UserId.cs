namespace Ilvi.Modules.AmoCrm.Domain.Users;

public readonly record struct UserId(long Value)
{
    public static UserId Empty => new(0);
    public static UserId From(long value) => new(value);
    
    // String çevrimi gerekirse diye
    public override string ToString() => Value.ToString();
}