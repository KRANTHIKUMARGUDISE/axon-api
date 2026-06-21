namespace Axon.Core.DTOs.Pipelines;

public class ValidateResponse
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
}
