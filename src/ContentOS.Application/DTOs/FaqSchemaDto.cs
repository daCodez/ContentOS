using System;

namespace ContentOS.Application.DTOs;

public class FaqSchemaDto
{
    public string[] Questions { get; set; } = Array.Empty<string>();
    public string[] Answers { get; set; } = Array.Empty<string>();
    public string SchemaType { get; set; } = "FAQPage";
}