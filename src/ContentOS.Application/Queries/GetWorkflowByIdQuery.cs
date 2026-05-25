using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ContentOS.Application.DTOs;
using MediatR;

namespace ContentOS.Application.Queries;

public record GetWorkflowByIdQuery(Guid Id) : IRequest<WorkflowDto>;