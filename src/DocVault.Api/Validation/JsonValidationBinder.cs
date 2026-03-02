using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocVault.Api.Validation;

/// <summary>
/// Reads the request body as a <see cref="JsonDocument"/>, validates every declared
/// property's JSON type individually, and collects ALL type errors before throwing.
/// This replaces the default Minimal API JSON binding, which stops at the first error.
/// </summary>
public static class JsonValidationBinder
{
  // Mirror the defaults used by Minimal API JSON binding (Web preset = camelCase + case-insensitive)
  private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

  public static async ValueTask<T?> BindAsync<T>(HttpContext context) where T : class
  {
    context.Request.EnableBuffering();

    string body;
    using (var reader = new StreamReader(context.Request.Body, leaveOpen: true))
      body = await reader.ReadToEndAsync(context.RequestAborted);

    context.Request.Body.Position = 0;

    if (string.IsNullOrWhiteSpace(body))
      throw new JsonBindingException(
        new Dictionary<string, string[]> { ["body"] = ["Request body must not be empty."] });

    JsonDocument doc;
    try
    {
      doc = JsonDocument.Parse(body);
    }
    catch (JsonException ex)
    {
      throw new JsonBindingException(
        new Dictionary<string, string[]> { ["body"] = [$"Invalid JSON: {ex.Message}"] });
    }

    using (doc)
    {
      var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

      foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        var jsonName = GetJsonName(prop);

        // Missing properties are intentionally skipped — NotEmpty/NotNull FluentValidation
        // rules report those with richer messages than we could produce here.
        if (!doc.RootElement.TryGetProperty(jsonName, out var element))
          continue;

        try
        {
          JsonSerializer.Deserialize(element.GetRawText(), prop.PropertyType, _options);
        }
        catch (JsonException)
        {
          errors[prop.Name] = [$"'{jsonName}' must be a valid {FriendlyTypeName(prop.PropertyType)}."];
        }
      }

      if (errors.Count > 0)
        throw new JsonBindingException(errors);

      return JsonSerializer.Deserialize<T>(body, _options);
    }
  }

  private static string GetJsonName(PropertyInfo prop)
  {
    var attr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
    return attr?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(prop.Name);
  }

  private static string FriendlyTypeName(Type type)
  {
    if (type == typeof(string)) return "string";
    if (type == typeof(bool) || type == typeof(bool?)) return "boolean";
    if (type == typeof(int) || type == typeof(long) ||
        type == typeof(int?) || type == typeof(long?)) return "number";

    if (type.IsGenericType)
    {
      var inner = type.GetGenericArguments().FirstOrDefault();
      return inner is not null ? $"array of {FriendlyTypeName(inner)}" : "array";
    }

    return type.Name;
  }
}
