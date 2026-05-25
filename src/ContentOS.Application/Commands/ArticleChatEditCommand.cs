using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Commands;

public record ArticleChatEditCommand(
    Guid ArticleId,
    string Message,
    string Scope,
    string? TargetSectionId,
    string? SelectedText) : IRequest<ArticleEditRequestDto>;
