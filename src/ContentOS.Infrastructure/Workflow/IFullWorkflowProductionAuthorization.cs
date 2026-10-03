using System.Text.Json;
namespace ContentOS.Infrastructure.Workflow;
/// <summary>Trusted, explicitly configured local execution scope; never derived from model output or input flags.</summary>
public interface IFullWorkflowProductionAuthorization
{
    void ValidateSpecification(FullWorkflowSpecification specification);
    void ValidateExecution(FullWorkflowSpecification specification,JsonElement frozenInput,Guid? existingRunId);
}
