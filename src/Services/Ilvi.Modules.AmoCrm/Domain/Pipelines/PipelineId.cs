namespace Ilvi.Modules.AmoCrm.Domain.Pipelines;

public readonly record struct PipelineId(long Value)
{
    public static PipelineId From(long value) => new(value);
}