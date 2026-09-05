using System.Text.Json;
using Wildfire.Core;

namespace Wildfire.Unity;

internal static class ShaderSnapshotParameters
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] RequiredProperties = JsonSerializer
        .SerializeToElement(FireSimParameters.Default, JsonOptions)
        .EnumerateObject()
        .Select(property => property.Name)
        .ToArray();

    public static FireSimParameters? Read(JsonElement root)
    {
        if (!root.TryGetProperty("parameters", out JsonElement parameters) || parameters.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        foreach (string name in RequiredProperties)
        {
            if (!parameters.TryGetProperty(name, out _))
            {
                throw new InvalidDataException($"Fixture parameters must include '{name}'; omit the whole parameters object to use Core defaults.");
            }
        }

        return parameters.Deserialize<FireSimParameters>(JsonOptions);
    }
}
