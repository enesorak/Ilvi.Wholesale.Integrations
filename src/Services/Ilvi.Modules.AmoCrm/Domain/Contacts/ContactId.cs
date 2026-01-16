namespace Ilvi.Modules.AmoCrm.Domain.Contacts;

public readonly record struct ContactId(long Value)
{
    public static ContactId Empty => new(0);
    public static ContactId From(long value) => new(value);
    public override string ToString() => Value.ToString();
}