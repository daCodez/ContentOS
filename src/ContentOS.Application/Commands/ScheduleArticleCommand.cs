using System;
using MediatR;

namespace ContentOS.Application.Commands;

public record ScheduleArticleCommand(Guid ArticleId, DateTime ScheduledFor) : IRequest;