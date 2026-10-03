using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Writing;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace ContentOS.Infrastructure.Articles;
public static class ArticleModelContext
{
    public static JsonSerializerOptions JsonOptions {get;}=Create();
    private static JsonSerializerOptions Create(){var options=new JsonSerializerOptions();options.Converters.Add(new StructuredArticleOnly());return options;}
    private sealed class StructuredArticleOnly:JsonConverter<GeneratedLongformArticle>
    {
        public override GeneratedLongformArticle Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)=>throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer,GeneratedLongformArticle value,JsonSerializerOptions options)=>JsonSerializer.Serialize(writer,EditorialRevisionService.ToDraft(value));
    }
}
