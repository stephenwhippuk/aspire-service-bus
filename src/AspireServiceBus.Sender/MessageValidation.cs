using System.Text.Json;

namespace AspireServiceBus.Sender;

public static class SendMessageRequestValidator
{
    public static string? Validate(SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Timestamp)
            || string.IsNullOrWhiteSpace(request.EntityName)
            || string.IsNullOrWhiteSpace(request.TargetApplication)
            || string.IsNullOrWhiteSpace(request.BodyJson))
        {
            return "Standard headers and body are required.";
        }

        if (request.WaitTimeSeconds is < 0)
        {
            return "Wait time seconds cannot be negative.";
        }

        try
        {
            using var _ = JsonDocument.Parse(request.BodyJson);
            return null;
        }
        catch (JsonException ex)
        {
            return $"Body must be valid JSON: {ex.Message}";
        }
    }
}
