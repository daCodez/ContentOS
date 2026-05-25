using System;
using System.Threading;
using System.Threading.Tasks;
using System;
using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Commands;

public record AnalyzeArticleSeoCommand(Guid ArticleId) : IRequest<SeoAnalysisDto>;