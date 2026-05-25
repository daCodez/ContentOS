using System;

namespace ContentOS.Application.DTOs;

public class QueueItemDto
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
}