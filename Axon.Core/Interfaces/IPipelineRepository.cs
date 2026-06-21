using Axon.Core.Enums;
using Axon.Core.Models;

namespace Axon.Core.Interfaces;

public interface IPipelineRepository
{
    Task<List<PipelineDefinition>> GetAllAsync(PipelineVisibility? visibility, string? userId);
    Task<PipelineDefinition?> GetByIdAsync(string id);
    Task CreateAsync(PipelineDefinition pipeline);
    Task UpdateAsync(PipelineDefinition pipeline);
    Task DeleteAsync(string id);
}
