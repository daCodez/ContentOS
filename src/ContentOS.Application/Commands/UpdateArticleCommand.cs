using System;
using MediatR;

namespace ContentOS.Application.Commands;

public record UpdateArticleCommand(Guid Id, string Title, string Content, string Summary, string Author, bool IsPublished) : IRequest;