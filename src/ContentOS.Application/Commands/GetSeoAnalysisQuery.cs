using System;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Commands;

public record GetSeoAnalysisQuery(Guid ArticleId) : IRequest<SeoAnalysisDto>;