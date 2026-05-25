using System;

namespace ContentOS.Application.DTOs;

public class SummaryDto
{
    public string SummaryText { get; set; } = string.Empty;
    public int OriginalLength { get; set; }
    public int SummaryLength { get; set; }
    public double CompressionRatio { get; set; }
}