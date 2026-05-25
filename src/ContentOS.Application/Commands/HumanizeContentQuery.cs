using System;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ContentOS.Application.Commands;

public record HumanizeContentQuery(string Content) : IRequest<HumanizedDto>;