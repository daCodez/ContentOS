using ContentOS.Application.Commands;
using ContentOS.Application.DTOs;
using ContentOS.Application.Queries;
using ContentOS.Infrastructure.Articles;
using MediatR;

namespace ContentOS.Infrastructure.Handlers;

public sealed class GetArticleEditingSessionQueryHandler : IRequestHandler<GetArticleEditingSessionQuery, ArticleEditingSessionDto>
{
    private readonly IArticleEditingService _service;

    public GetArticleEditingSessionQueryHandler(IArticleEditingService service)
    {
        _service = service;
    }

    public Task<ArticleEditingSessionDto> Handle(GetArticleEditingSessionQuery request, CancellationToken cancellationToken)
        => _service.GetSessionAsync(request.ArticleId, cancellationToken);
}

public sealed class ArticleChatEditCommandHandler : IRequestHandler<ArticleChatEditCommand, ArticleEditRequestDto>
{
    private readonly IArticleEditingService _service;

    public ArticleChatEditCommandHandler(IArticleEditingService service)
    {
        _service = service;
    }

    public Task<ArticleEditRequestDto> Handle(ArticleChatEditCommand request, CancellationToken cancellationToken)
        => _service.CreateEditProposalAsync(request.ArticleId, request.Message, request.Scope, request.TargetSectionId, request.SelectedText, cancellationToken);
}

public sealed class AcceptArticleEditCommandHandler : IRequestHandler<AcceptArticleEditCommand>
{
    private readonly IArticleEditingService _service;

    public AcceptArticleEditCommandHandler(IArticleEditingService service)
    {
        _service = service;
    }

    public Task Handle(AcceptArticleEditCommand request, CancellationToken cancellationToken)
        => _service.AcceptEditAsync(request.ArticleId, request.RequestId, request.AppliedBy, cancellationToken);
}

public sealed class RejectArticleEditCommandHandler : IRequestHandler<RejectArticleEditCommand>
{
    private readonly IArticleEditingService _service;

    public RejectArticleEditCommandHandler(IArticleEditingService service)
    {
        _service = service;
    }

    public Task Handle(RejectArticleEditCommand request, CancellationToken cancellationToken)
        => _service.RejectEditAsync(request.ArticleId, request.RequestId, request.RejectedBy, cancellationToken);
}
