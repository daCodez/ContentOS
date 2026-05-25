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

public record LoginCommand(string Username, string Password) : IRequest<AuthResponseDto>;