using System.Text.Json;

namespace Jyro.Cli;

/// <summary>
/// Represents a single mismatch found during JSON comparison.
/// </summary>
internal sealed record JsonMismatch(string Path, string Expected, string Actual);

/// <summary>
/// Semantic JSON comparison utility. Compares two JSON documents structurally
/// with numeric tolerance for floating-point differences.
/// </summary>
internal static class JsonComparer
{
    /// <summary>
    /// Compares two JSON strings semantically, returning a list of mismatches.
    /// An empty list means the documents are equivalent.
    /// </summary>
    public static List<JsonMismatch> Compare(string expectedJson, string actualJson)
    {
        using var expected = JsonDocument.Parse(expectedJson);
        using var actual = JsonDocument.Parse(actualJson);
        var mismatches = new List<JsonMismatch>();
        CompareElements(expected.RootElement, actual.RootElement, "$", mismatches);
        return mismatches;
    }

    private static void CompareElements(JsonElement expected, JsonElement actual, string path, List<JsonMismatch> mismatches)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            // Allow number type differences (int vs float) if values match
            if (IsNumeric(expected) && IsNumeric(actual))
            {
                CompareNumbers(expected, actual, path, mismatches);
                return;
            }
            mismatches.Add(new(path, $"{expected.ValueKind}: {expected.GetRawText()}", $"{actual.ValueKind}: {actual.GetRawText()}"));
            return;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                CompareObjects(expected, actual, path, mismatches);
                break;
            case JsonValueKind.Array:
                CompareArrays(expected, actual, path, mismatches);
                break;
            case JsonValueKind.Number:
                CompareNumbers(expected, actual, path, mismatches);
                break;
            case JsonValueKind.String:
                var es = expected.GetString();
                var a = actual.GetString();
                if (es != a)
                {
                    mismatches.Add(new(path, es ?? "null", a ?? "null"));
                }

                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                if (expected.GetBoolean() != actual.GetBoolean())
                {
                    mismatches.Add(new(path, expected.GetRawText(), actual.GetRawText()));
                }

                break;
                // Null == Null: no mismatch
        }
    }

    private static void CompareObjects(JsonElement expected, JsonElement actual, string path, List<JsonMismatch> mismatches)
    {
        var expectedProps = new HashSet<string>();
        foreach (var prop in expected.EnumerateObject())
        {
            expectedProps.Add(prop.Name);
            if (actual.TryGetProperty(prop.Name, out var actualProp))
            {
                CompareElements(prop.Value, actualProp, $"{path}.{prop.Name}", mismatches);
            }
            else
            {
                mismatches.Add(new($"{path}.{prop.Name}", prop.Value.GetRawText(), "(missing)"));
            }
        }

        foreach (var prop in actual.EnumerateObject())
        {
            if (!expectedProps.Contains(prop.Name))
            {
                mismatches.Add(new($"{path}.{prop.Name}", "(missing)", prop.Value.GetRawText()));
            }
        }
    }

    private static void CompareArrays(JsonElement expected, JsonElement actual, string path, List<JsonMismatch> mismatches)
    {
        var expectedLen = expected.GetArrayLength();
        var actualLen = actual.GetArrayLength();

        if (expectedLen != actualLen)
        {
            mismatches.Add(new($"{path}.length", expectedLen.ToString(), actualLen.ToString()));
        }

        var minLen = Math.Min(expectedLen, actualLen);
        for (int i = 0; i < minLen; i++)
        {
            CompareElements(expected[i], actual[i], $"{path}[{i}]", mismatches);
        }
    }

    private static void CompareNumbers(JsonElement expected, JsonElement actual, string path, List<JsonMismatch> mismatches)
    {
        var e = expected.GetDouble();
        var a = actual.GetDouble();
        if (e != a)
        {
            var mag = Math.Max(Math.Abs(e), Math.Abs(a));
            var tol = mag == 0 ? 1e-15 : mag * 1e-10;
            if (Math.Abs(e - a) > tol)
            {
                mismatches.Add(new(path, expected.GetRawText(), actual.GetRawText()));
            }
        }
    }

    private static bool IsNumeric(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number;
}
