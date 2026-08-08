using System.Text.Json;
using AspireServiceBus.Sender;
using Azure.Messaging.ServiceBus;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

var queueName = builder.Configuration["ServiceBus:QueueName"] ?? "default-queue";
var connectionString = ServiceBusConnectionSettings.ResolveConnectionString(builder.Configuration);
var localLogPath = builder.Configuration["Logging:LocalFilePath"] ?? Environment.GetEnvironmentVariable("SERVICEBUS_LOG_FILE");

if (string.IsNullOrWhiteSpace(connectionString))
{
	builder.Services.AddSingleton<ServiceBusClient>(_ => null!);
}
else
{
	builder.Services.AddSingleton(new ServiceBusClient(connectionString));
}

var app = builder.Build();

app.MapGet("/", () => Results.Content(GetSenderPageHtml(), "text/html"));

app.MapPost("/send", async (SendMessageRequest request, ServiceBusClient? client, CancellationToken cancellationToken) =>
{
		var validationError = SendMessageRequestValidator.Validate(request);
		if (validationError is not null)
		{
			return Results.BadRequest(new { error = validationError });
		}

		if (client is null)
		{
			await AppendLocalLogAsync(localLogPath, new
			{
				timestamp = DateTimeOffset.UtcNow,
				service = "sender",
				action = "send-failed",
				queue = queueName,
				error = "Service Bus connection is not available yet. The service will stay running and retry once the emulator connection is configured."
			}, cancellationToken);

			return Results.Json(new { error = "Service Bus connection is not available yet. The service will stay running and retry once the emulator connection is configured." }, statusCode: 503);
		}

		try
		{
			await using var sender = client.CreateSender(queueName);

			var message = new ServiceBusMessage(request.BodyJson)
			{
				ContentType = "application/json",
				MessageId = Guid.NewGuid().ToString("N")
			};

			message.ApplicationProperties["timestamp"] = request.Timestamp;
			message.ApplicationProperties["entity-name"] = request.EntityName;
			message.ApplicationProperties["target-application"] = request.TargetApplication;

			if (request.CustomHeaders is not null)
			{
				foreach (var (key, value) in request.CustomHeaders)
				{
					if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
					{
						message.ApplicationProperties[key] = value;
					}
				}
			}

			await sender.SendMessageAsync(message, cancellationToken);
			await AppendLocalLogAsync(localLogPath, new
			{
				timestamp = DateTimeOffset.UtcNow,
				service = "sender",
				action = "send",
				queue = queueName,
				messageId = message.MessageId,
				headers = message.ApplicationProperties
			}, cancellationToken);

			return Results.Ok(new { status = "sent", queue = queueName, messageId = message.MessageId });
		}
		catch (ServiceBusException ex) when (IsTransientServiceBusFailure(ex))
		{
			await AppendLocalLogAsync(localLogPath, new
			{
				timestamp = DateTimeOffset.UtcNow,
				service = "sender",
				action = "send-failed",
				queue = queueName,
				error = "Unable to reach the Service Bus emulator. Verify that the emulator is running and the connection is available."
			}, cancellationToken);

			return Results.Json(
				new { error = "Unable to reach the Service Bus emulator. Verify that the emulator is running and the connection is available." },
				statusCode: 503);
		}
		catch (Exception ex)
		{
			await AppendLocalLogAsync(localLogPath, new
			{
				timestamp = DateTimeOffset.UtcNow,
				service = "sender",
				action = "send-failed",
				queue = queueName,
				error = ex.Message
			}, cancellationToken);

			return Results.Json(new { error = $"Send failed: {ex.Message}" }, statusCode: 500);
		}
	});

app.Run();

static string GetSenderPageHtml() => """
<!doctype html>
<html lang="en">
<head>
	<meta charset="utf-8" />
	<meta name="viewport" content="width=device-width, initial-scale=1" />
	<title>Aspire Service Bus Sender</title>
	<style>
		body { font-family: sans-serif; margin: 2rem auto; max-width: 920px; padding: 0 1rem; }
		h1 { margin-bottom: 0.5rem; }
		.row { display: grid; grid-template-columns: 220px 1fr; gap: 0.75rem; margin-bottom: 0.75rem; align-items: center; }
		input, textarea, button { font: inherit; padding: 0.55rem; }
		textarea { min-height: 220px; }
		.headers { border: 1px solid #ddd; border-radius: 8px; padding: 0.75rem; margin-bottom: 1rem; }
		.custom-row { display: grid; grid-template-columns: 1fr 1fr auto; gap: 0.5rem; margin-bottom: 0.5rem; }
		.status { margin-top: 1rem; font-weight: 600; }
		.ok { color: #0b7a2a; }
		.err { color: #b42318; }
		.field-error { border-color: #b42318; outline-color: #b42318; }
	</style>
</head>
<body>
	<h1>Message Sender</h1>
	<p>Queue: <strong>default-queue</strong></p>

	<div class="headers">
		<div class="row">
			<label for="timestamp">timestamp</label>
			<input id="timestamp" />
		</div>
		<div class="row">
			<label for="entityName">entity-name</label>
			<input id="entityName" value="default-queue" />
		</div>
		<div class="row">
			<label for="targetApplication">target-application</label>
			<input id="targetApplication" value="receiver" />
		</div>
	</div>

	<h2>Custom Headers</h2>
	<div id="customHeaders"></div>
	<button type="button" id="addHeader">Add Header</button>

	<h2>Body (JSON)</h2>
	<textarea id="bodyJson">{
	"message": "hello"
}</textarea>

	<div style="margin-top: 1rem;">
		<button type="button" id="sendBtn">Send</button>
	</div>

	<div id="validationSummary" class="status err" style="display:none;"></div>
	<div id="status" class="status"></div>

	<script>
		const timestampInput = document.getElementById('timestamp');
		const entityNameInput = document.getElementById('entityName');
		const targetAppInput = document.getElementById('targetApplication');
		const bodyInput = document.getElementById('bodyJson');
		const addHeaderBtn = document.getElementById('addHeader');
		const sendBtn = document.getElementById('sendBtn');
		const customHeadersDiv = document.getElementById('customHeaders');
		const validationSummary = document.getElementById('validationSummary');
		const statusDiv = document.getElementById('status');

		const validationInputs = [timestampInput, entityNameInput, targetAppInput, bodyInput];
		timestampInput.value = new Date().toISOString();

		function addHeaderRow(key = '', value = '') {
			const row = document.createElement('div');
			row.className = 'custom-row';
			row.innerHTML = `
				<input placeholder="header key" value="${key}">
				<input placeholder="header value" value="${value}">
				<button type="button">Remove</button>
			`;
			row.querySelector('button').addEventListener('click', () => {
				row.remove();
				validateForm();
			});
			customHeadersDiv.appendChild(row);
		}

		function getCustomHeaders() {
			const headers = {};
			customHeadersDiv.querySelectorAll('.custom-row').forEach(row => {
				const inputs = row.querySelectorAll('input');
				const key = inputs[0].value.trim();
				const value = inputs[1].value.trim();
				if (key && value) headers[key] = value;
			});
			return headers;
		}

		function setFieldState(input, hasError) {
			input.classList.toggle('field-error', hasError);
		}

		function validateForm() {
			const errors = [];
			const requiredFields = [
				{ input: timestampInput, label: 'timestamp' },
				{ input: entityNameInput, label: 'entity-name' },
				{ input: targetAppInput, label: 'target-application' }
			];

			requiredFields.forEach(({ input, label }) => {
				if (!input.value.trim()) {
					errors.push(`${label} is required`);
					setFieldState(input, true);
				} else {
					setFieldState(input, false);
				}
			});

			if (!bodyInput.value.trim()) {
				errors.push('body is required');
				setFieldState(bodyInput, true);
			} else {
				try {
					JSON.parse(bodyInput.value);
					setFieldState(bodyInput, false);
				} catch (err) {
					errors.push(`body must be valid JSON: ${err.message}`);
					setFieldState(bodyInput, true);
				}
			}

			sendBtn.disabled = errors.length > 0;
			validationSummary.style.display = errors.length ? 'block' : 'none';
			validationSummary.textContent = errors.join(' • ');
			return errors.length === 0;
		}

		addHeaderBtn.addEventListener('click', () => {
			addHeaderRow();
			validateForm();
		});

		validationInputs.forEach(input => input.addEventListener('input', validateForm));
		validateForm();

		sendBtn.addEventListener('click', async () => {
			statusDiv.className = 'status';
			statusDiv.textContent = '';

			if (!validateForm()) {
				statusDiv.classList.add('err');
				statusDiv.textContent = 'Please fix the validation errors before sending.';
				return;
			}

			const payload = {
				timestamp: timestampInput.value,
				entityName: entityNameInput.value,
				targetApplication: targetAppInput.value,
				bodyJson: bodyInput.value,
				customHeaders: getCustomHeaders()
			};

			try {
				const response = await fetch('/send', {
					method: 'POST',
					headers: { 'Content-Type': 'application/json' },
					body: JSON.stringify(payload)
				});

				const data = await response.json();

				if (response.ok) {
					statusDiv.classList.add('ok');
					statusDiv.textContent = `Sent to ${data.queue}. messageId=${data.messageId}`;
					timestampInput.value = new Date().toISOString();
					validateForm();
				} else {
					statusDiv.classList.add('err');
					statusDiv.textContent = data.error || 'Send failed';
				}
			} catch (err) {
				statusDiv.classList.add('err');
				statusDiv.textContent = err.message || 'Send failed';
			}
		});
	</script>
</body>
</html>
""";

static bool IsTransientServiceBusFailure(ServiceBusException ex)
{
	return ex.IsTransient || ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
}

static async Task AppendLocalLogAsync(string? logFilePath, object payload, CancellationToken cancellationToken)
{
	if (string.IsNullOrWhiteSpace(logFilePath))
	{
		return;
	}

	var directory = Path.GetDirectoryName(logFilePath);
	if (!string.IsNullOrWhiteSpace(directory))
	{
		Directory.CreateDirectory(directory);
	}

	await File.AppendAllTextAsync(logFilePath, JsonSerializer.Serialize(payload) + Environment.NewLine, cancellationToken);
}

public sealed record SendMessageRequest(
		string Timestamp,
		string EntityName,
		string TargetApplication,
		string BodyJson,
		Dictionary<string, string>? CustomHeaders);
