using System;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Application.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;

namespace ContentOS.Application.Commands;

public record GenerateMetaTagsQuery(string Content) : IRequest<MetaTagsDto>;