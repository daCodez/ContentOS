using MediatR;

namespace ContentOS.Application.Commands;

public record AcceptArticleEditCommand(Guid ArticleId, Guid RequestId, string AppliedBy) : IRequest;
