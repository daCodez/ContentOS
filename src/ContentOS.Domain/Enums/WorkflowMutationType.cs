namespace ContentOS.Domain.Enums;

public static class WorkflowMutationType
{
    public const string InjectManualTask = "inject-manual-task";
    public const string ExecuteTask = "execute-task";
    public const string ResetTask = "reset-task";
    public const string AcceptTask = "accept-task";
    public const string RejectTask = "reject-task";
    public const string PauseWorkflow = "pause-workflow";
    public const string ResumeWorkflow = "resume-workflow";
    public const string ReopenWorkflow = "reopen-workflow";
    public const string ReorderTask = "reorder-task";
}
