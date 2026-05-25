using ContentOS.Domain.Entities;
using MediatR;

namespace ContentOS.Application.Commands;

public class CreateSiteCommand : IRequest<Site>
{
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Niche { get; set; } = string.Empty;
    public string DefaultTone { get; set; } = string.Empty;
    public string PlatformType { get; set; } = "WordPress";
}