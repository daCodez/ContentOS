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

public record GenerateTitleIdeasQuery(string Content, int Count) : IRequest<TitleIdeasDto>;