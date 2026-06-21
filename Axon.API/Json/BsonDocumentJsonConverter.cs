using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.IO;

namespace Axon.API.Json;

// Lets BsonDocument-typed properties (e.g. Delivery.Inputs) round-trip as plain JSON
// over the API while storing as Mongo's native arbitrary-document type — avoids
// MongoDB.Bson's ObjectSerializer rejecting System.Text.Json.JsonElement, which is
// what Dictionary<string, object> properties deserialize their values into by default.
public class BsonDocumentJsonConverter : JsonConverter<BsonDocument>
{
    public override BsonDocument? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.Null) return null;
        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        return BsonDocument.Parse(jsonDoc.RootElement.GetRawText());
    }

    public override void Write(Utf8JsonWriter writer, BsonDocument? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        var json = value.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });
        using var jsonDoc = JsonDocument.Parse(json);
        jsonDoc.RootElement.WriteTo(writer);
    }
}
