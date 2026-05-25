using ContentOS.Application.Commands;
using ContentOS.Application.DTOs;
using ContentOS.Infrastructure.Articles;
using MediatR;

namespace ContentOS.Infrastructure.Handlers;

public sealed class UpdateArticleFieldLocksCommandHandler : IRequestHandler<UpdateArticleFieldLocksCommand, ArticleFieldLocksDto>
{
    private readonly IArticleEditingService _service;

    public UpdateArticleFieldLocksCommandHandler(IArticleEditingService service)
    {
        _service = service;
    }

    public Task<ArticleFieldLocksDto> Handle(UpdateArticleFieldLocksCommand request, CancellationToken cancellationToken)
        => _service.UpdateFieldLocksAsync(request.ArticleId, request.Locks, cancellationToken);
}
