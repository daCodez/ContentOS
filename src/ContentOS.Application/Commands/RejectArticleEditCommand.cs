using MediatR;

namespace ContentOS.Application.Commands;

public record RejectArticleEditCommand(Guid ArticleId, Guid RequestId, string RejectedBy) : IRequest;
