using System.Text.Json;

namespace ResumeScreening.Api.Services;

internal static class JobScreeningJsonSchema
{
    private static readonly JsonElement ScreeningSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["grade", "summary", "evidence"],
          "properties": {
            "grade": {
              "type": "string",
              "enum": ["A", "B", "C"]
            },
            "summary": {
              "type": "string"
            },
            "evidence": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["pageNumber", "quote"],
                "properties": {
                  "pageNumber": { "type": "integer", "minimum": 1 },
                  "quote": { "type": "string" }
                }
              }
            }
          }
        }
        """).RootElement.Clone();

    public static JsonElement Value { get; } = Create();

    private static JsonElement Create()
    {
        var resumeSchema = ResumeAnalysisJsonSchema.Value;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "object");
            writer.WritePropertyName("properties");
            writer.WriteStartObject();
            writer.WritePropertyName("resumeAnalysis");
            WriteResumeSchemaWithoutDefinitions(writer, resumeSchema);
            writer.WritePropertyName("screening");
            ScreeningSchema.WriteTo(writer);
            writer.WriteEndObject();
            writer.WritePropertyName("required");
            writer.WriteStartArray();
            writer.WriteStringValue("resumeAnalysis");
            writer.WriteStringValue("screening");
            writer.WriteEndArray();
            writer.WriteBoolean("additionalProperties", false);
            writer.WritePropertyName("$defs");
            resumeSchema.GetProperty("$defs").WriteTo(writer);
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static void WriteResumeSchemaWithoutDefinitions(
        Utf8JsonWriter writer,
        JsonElement resumeSchema)
    {
        writer.WriteStartObject();
        foreach (var property in resumeSchema.EnumerateObject())
        {
            if (property.NameEquals("$defs"))
            {
                continue;
            }

            property.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}
