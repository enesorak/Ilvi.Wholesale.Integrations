namespace Ilvi.Modules.AmoCrm.Domain.Tasks;

public readonly record struct TaskId(long Value)
{
    public static TaskId Empty => new(0);
    public static TaskId From(long value) => new(value);
}