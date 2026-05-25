using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetWorkflowsQuery(
    string? Name = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 10,
    string SortBy = "CreatedAt",
    bool SortAscending = true) : IRequest<PaginatedResultDto<WorkflowDto>>;