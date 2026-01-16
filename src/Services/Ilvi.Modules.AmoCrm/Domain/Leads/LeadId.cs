namespace Ilvi.Modules.AmoCrm.Domain.Leads;

public readonly record struct LeadId(long Value)
{
    public static LeadId Empty => new(0);
    public static LeadId From(long value) => new(value);
    public override string ToString() => Value.ToString();
}