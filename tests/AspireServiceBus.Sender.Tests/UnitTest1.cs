using AspireServiceBus.Sender;
using Microsoft.Extensions.Configuration;

namespace AspireServiceBus.Sender.Tests;

public class SendMessageRequestValidatorTests
{
    [Fact]
    public void Validate_ReturnsError_ForMissingRequiredFields()
    {
        var request = new SendMessageRequest("", "", "", "{}", null);

        var error = SendMessageRequestValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Contains("required", error);
    }

    [Fact]
    public void Validate_ReturnsError_ForInvalidJson()
    {
        var request = new SendMessageRequest("2026-08-08T00:00:00Z", "entity", "receiver", "{not-json}", null);

        var error = SendMessageRequestValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Contains("valid JSON", error);
    }

    [Fact]
    public void Validate_ReturnsNull_ForValidRequest()
    {
        var request = new SendMessageRequest("2026-08-08T00:00:00Z", "entity", "receiver", "{\"value\":1}", new Dictionary<string, string> { { "trace", "abc" } });

        var error = SendMessageRequestValidator.Validate(request);

        Assert.Null(error);
    }

    [Fact]
    public void ResolveConnectionString_UsesEnvironmentStyleConfigKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings__servicebus"] = "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test"
            })
            .Build();

        var connectionString = ServiceBusConnectionSettings.ResolveConnectionString(configuration);

        Assert.Equal("Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test", connectionString);
    }

    [Fact]
    public void CreateClientErrorMessage_ReturnsGenericMessage()
    {
        var exception = new InvalidOperationException("sensitive details");

        var message = SenderEndpoints.CreateClientErrorMessage(exception);

        Assert.Equal("An unexpected error occurred while sending the message.", message);
        Assert.DoesNotContain("sensitive details", message, StringComparison.OrdinalIgnoreCase);
    }
}