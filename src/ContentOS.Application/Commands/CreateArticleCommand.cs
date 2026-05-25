using System;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;
using System;
using MediatR;

namespace ContentOS.Application.Commands;

public record CreateArticleCommand(string Title, string Content, string Summary, string Author) : IRequest<Guid>;