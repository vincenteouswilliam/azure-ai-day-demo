// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AspNetCore.Mvc.Filters;

namespace MinimalApi.Extensions;

internal static class WebApplicationExtensions
{
    internal static WebApplication MapApi(this WebApplication app)
    {
        var api = app.MapGroup("api");

        // NTT DATA bot streaming endpoint
        api.MapPost("openai/chat", OnPostChatPromptAsync);

        // Long-form chat w/ contextual history endpoint
        api.MapPost("chat", OnPostChatAsync);

        // Upload a document
        api.MapPost("documents", OnPostDocumentAsync);

        // Get all documents
        api.MapGet("documents", OnGetDocumentsAsync);

        // Get DALL-E image result from prompt
        api.MapPost("images", OnPostImagePromptAsync);

        // Connection to Postgresql DB
        api.MapGet("db", OnGetDataFromDBAsync);

        // Mail connection
        api.MapGet("mail", OnGetEmailNotificationAsync);

        api.MapGet("enableLogout", OnGetEnableLogout);

        return app;
    }

    private static IResult OnGetEnableLogout(HttpContext context)
    {
        var header = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-ID"];
        var enableLogout = !string.IsNullOrEmpty(header);

        return TypedResults.Ok(enableLogout);
    }

    private static async IAsyncEnumerable<ChatChunkResponse> OnPostChatPromptAsync(
        PromptRequest prompt,
        OpenAIClient client,
        IConfiguration config,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var deploymentId = config["AZURE_OPENAI_CHATGPT_DEPLOYMENT"];
        var response = await client.GetChatCompletionsStreamingAsync(
            new ChatCompletionsOptions
            {
                DeploymentName = deploymentId,
                Messages =
                {
                    new ChatRequestSystemMessage("""
                        You're an AI assistant for developers, helping them write code more efficiently.
                        You're name is **NTT DATA bot** and you're an expert Blazor developer.
                        You're also an expert in ASP.NET Core, C#, TypeScript, and even JavaScript.
                        You will always reply with a Markdown formatted response.
                        """),
                    new ChatRequestUserMessage("What's your name?"),
                    new ChatRequestAssistantMessage("Hi, my name is **NTT DATA bot**! Nice to meet you."),
                    new ChatRequestUserMessage(prompt.Prompt)
                }
            }, cancellationToken);

        await foreach (var choice in response.WithCancellation(cancellationToken))
        {
            if (choice.ContentUpdate is { Length: > 0 })
            {
                yield return new ChatChunkResponse(choice.ContentUpdate.Length, choice.ContentUpdate);
            }
        }
    }

    private static async Task<IResult> OnPostChatAsync(
        ChatRequest request,
        ReadRetrieveReadChatService chatService,
        CancellationToken cancellationToken)
    {
        if (request is { History.Length: > 0 })
        {
            var response = await chatService.ReplyAsync(
                request.History, request.Overrides, cancellationToken);

            return TypedResults.Ok(response);
        }

        return Results.BadRequest();
    }

    private static async Task<IResult> OnPostDocumentAsync(
        [FromForm] IFormFileCollection files,
        [FromServices] AzureBlobStorageService service,
        [FromServices] ILogger<AzureBlobStorageService> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Upload documents");

        var response = await service.UploadFilesAsync(files, cancellationToken);

        logger.LogInformation("Upload documents: {x}", response);

        return TypedResults.Ok(response);
    }

    private static async IAsyncEnumerable<DocumentResponse> OnGetDocumentsAsync(
        BlobContainerClient client,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var blob in client.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            if (blob is not null and { Deleted: false })
            {
                var props = blob.Properties;
                var baseUri = client.Uri;
                var builder = new UriBuilder(baseUri);
                builder.Path += $"/{blob.Name}";

                var metadata = blob.Metadata;
                var documentProcessingStatus = GetMetadataEnumOrDefault<DocumentProcessingStatus>(
                    metadata, nameof(DocumentProcessingStatus), DocumentProcessingStatus.NotProcessed);
                var embeddingType = GetMetadataEnumOrDefault<EmbeddingType>(
                    metadata, nameof(EmbeddingType), EmbeddingType.AzureSearch);

                yield return new(
                    blob.Name,
                    props.ContentType,
                    props.ContentLength ?? 0,
                    props.LastModified,
                    builder.Uri,
                    documentProcessingStatus,
                    embeddingType);

                static TEnum GetMetadataEnumOrDefault<TEnum>(
                    IDictionary<string, string> metadata,
                    string key,
                    TEnum @default) where TEnum : struct => metadata.TryGetValue(key, out var value)
                        && Enum.TryParse<TEnum>(value, out var status)
                            ? status
                            : @default;
            }
        }
    }

    private static async Task<IResult> OnPostImagePromptAsync(
        PromptRequest prompt,
        OpenAIClient client,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        var result = await client.GetImageGenerationsAsync(new ImageGenerationOptions
        {
            Prompt = prompt.Prompt,
        },
        cancellationToken);

        var imageUrls = result.Value.Data.Select(i => i.Url).ToList();
        var response = new ImageResponse(result.Value.Created, imageUrls);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> OnGetDataFromDBAsync(IConfiguration config)
    {
        var connectionString = config["AZURE_POSTGRESQL_CONNECTION_STRING"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Results.Problem("Connection string not found in environment variable.");
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await connection.CloseAsync();
            return Results.Ok("PostgreSQL connection successful!");
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error connecting to database: {ex.Message}");
        }
    }

    private static async Task<IResult> OnGetEmailNotificationAsync(IConfiguration config)
    {
        // Variables
        var smtpHost = config["MAIL_SMTP_HOST"];
        var smtpPort = int.TryParse(config["MAIL_SMTP_PORT"], out int port) ? port : 587;
        var senderAddress = config["MAIL_SENDER_EMAIL_ADDRESS"];
        var senderPassword = config["MAIL_SENDER_EMAIL_PASSWORD"];
        var senderDisplayName = config["MAIL_SENDER_DISPLAY_NAME"];
        var dummyRecipient = config["MAIL_DUMMY_RECIPIENT_ADDRESS"] ?? throw new ArgumentNullException("MAIL_DUMMY_RECIPIENT_ADDRESS");

        try
        {
            if (string.IsNullOrWhiteSpace(senderAddress))
            {
                return Results.Problem("Sender email address is not configured.");
            }

            // Set SMTP Client
            using var smtpClient = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderAddress, senderPassword)
            };

            // Set Mail Message
            using var mailMessage = new MailMessage
            {
                From = new MailAddress(senderAddress, senderDisplayName),
                Subject = "Test Email from AI App",
                Body = "This is a test email from AI App for the purpose of checking mail connection",
                IsBodyHtml = false
            };

            // Add Recipient
            if (string.IsNullOrEmpty(dummyRecipient))
            {
                return Results.Problem("Dummy target recipient is empty");
            }
            mailMessage.To.Add(dummyRecipient);

            // Send the email
            await smtpClient.SendMailAsync(mailMessage);
            return Results.Ok("Email connection successful!");
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to send email: {ex.Message}");
        }
    }
}
