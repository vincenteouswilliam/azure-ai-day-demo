// Copyright (c) Microsoft. All rights reserved.

using Azure.Core;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;

namespace MinimalApi.Services;
#pragma warning disable SKEXP0011 // Mark members as static
#pragma warning disable SKEXP0001 // Mark members as static
public class ReadRetrieveReadChatService
{
    private readonly ISearchService _searchClient;
    private readonly Kernel _kernel;
    private readonly IConfiguration _configuration;
    private readonly IComputerVisionService? _visionService;
    private readonly TokenCredential? _tokenCredential;
    private readonly PostgresTicketService _ticketService;
    private readonly ILogger<ReadRetrieveReadChatService> _logger;

    public ReadRetrieveReadChatService(
        ILogger<ReadRetrieveReadChatService> logger,
        ISearchService searchClient,
        OpenAIClient client,
        IConfiguration configuration,
        PostgresTicketService ticketService,
        IComputerVisionService? visionService = null,
        TokenCredential? tokenCredential = null)
    {
        _searchClient = searchClient;
        var kernelBuilder = Kernel.CreateBuilder();

        if (configuration["UseAOAI"] == "false")
        {
            var deployment = configuration["OpenAiChatGptDeployment"];
            ArgumentNullException.ThrowIfNullOrWhiteSpace(deployment);
            kernelBuilder = kernelBuilder.AddOpenAIChatCompletion(deployment, client);

            var embeddingModelName = configuration["OpenAiEmbeddingDeployment"];
            ArgumentNullException.ThrowIfNullOrWhiteSpace(embeddingModelName);
            kernelBuilder = kernelBuilder.AddOpenAITextEmbeddingGeneration(embeddingModelName, client);
        }
        else
        {
            var deployedModelName = configuration["AzureOpenAiChatGptDeployment"];
            ArgumentNullException.ThrowIfNullOrWhiteSpace(deployedModelName);
            var embeddingModelName = configuration["AzureOpenAiEmbeddingDeployment"];
            if (!string.IsNullOrEmpty(embeddingModelName))
            {
                var endpoint = configuration["AzureOpenAiServiceEndpoint"];
                ArgumentNullException.ThrowIfNullOrWhiteSpace(endpoint);
                kernelBuilder = kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(embeddingModelName, endpoint, tokenCredential ?? new DefaultAzureCredential());
                kernelBuilder = kernelBuilder.AddAzureOpenAIChatCompletion(deployedModelName, endpoint, tokenCredential ?? new DefaultAzureCredential());
            }
        }

        _kernel = kernelBuilder.Build();
        _configuration = configuration;
        _visionService = visionService;
        _tokenCredential = tokenCredential;
        _ticketService = ticketService;
        _logger = logger;
    }

    public async Task<ChatAppResponse> ReplyAsync(
        ChatMessage[] history,
        RequestOverrides? overrides,
        CancellationToken cancellationToken = default)
    {
        var top = overrides?.Top ?? 3;
        var dbTop = overrides?.DBTop ?? 15;
        var queryMode = overrides?.QueryMode ?? QueryMode.Document; // Default to Document
        var useSemanticCaptions = overrides?.SemanticCaptions ?? false;
        var useSemanticRanker = overrides?.SemanticRanker ?? false;
        var excludeCategory = overrides?.ExcludeCategory ?? null;
        var filter = excludeCategory is null ? null : $"category ne '{excludeCategory}'";
        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var embedding = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        float[]? embeddings = null;
        var question = history.LastOrDefault(m => m.IsUser)?.Content is { } userQuestion
            ? userQuestion
            : throw new InvalidOperationException("Use question is null");

        string[]? followUpQuestionList = null;
        string? content = string.Empty;
        SupportingImageRecord[]? images = default;
        SupportingContentRecord[]? documentContentList = null;
        ChatMessageContentItemCollection? contentCollection = null;

        // step 0
        // put together related docs and conversation history to generate answer
        var answerChat = new ChatHistory(
            queryMode == QueryMode.Database
                ? "You are a system assistant who helps with customer support ticket queries. Be brief and precise in your answers."
                : "You are a system assistant who helps the company employees with their questions. Be brief in your answers"
        );

        // add chat history
        foreach (var message in history)
        {
            if (message.IsUser)
            {
                answerChat.AddUserMessage(message.Content);
            }
            else
            {
                answerChat.AddAssistantMessage(message.Content);
            }
        }

        // Process based on Document or Database
        if (queryMode == QueryMode.Database)
        {
            // Step 1: Generate SQL Query using semantic kernel
            var sqlQueryChat = new ChatHistory($@"You are a helpful AI assistant that generates SQL queries for a PostgreSQL database.
The table 'customer_support_tickets' has the following schema:
- ticket_id (INTEGER, PRIMARY KEY) NOT NULL
- customer_name (VARCHAR(255)) NOT NULL
- customer_email (VARCHAR(255)) NOT NULL
- customer_age (INTEGER) NOT NULL
- customer_gender (VARCHAR(50)) NOT NULL
- product_purchased (VARCHAR(255)) NOT NULL
- date_of_purchase (DATE) NOT NULL
- ticket_type (VARCHAR(100)) NOT NULL
- ticket_subject (VARCHAR(255)) NOT NULL
- ticket_description (TEXT) NOT NULL
- ticket_status (VARCHAR(50)) NOT NULL
- resolution (TEXT)
- ticket_priority (VARCHAR(50)) NOT NULL
- ticket_channel (VARCHAR(50)) NOT NULL
- first_response_time (TIMESTAMP)
- time_to_resolution (TIMESTAMP)
- customer_satisfaction_rating (FLOAT)

Generate a parameterized SQL query based on the user's prompt. Return a JSON object with:
- 'query': The SQL query with placeholders (e.g., @p1, @p2).
- 'parameters': A list of dictionary of parameter names and their values. For date/time-related value, construct the value in 'yyyy-MM-dd' format for date only and 'yyyy-MM-dd HH:mm:ss' format for datetime. Add a parameter named 'type' whose value corresponds with column data type (e.g., if @p1 is a placeholder for 'ticket_priority' column, 'type' parameter value should be 'VARCHAR')
Ensure the query is safe, uses ILIKE for text searches, and limits to {dbTop} results. For nullable columns, use IS NULL or IS NOT NULL to check whether a value exists or not. For date/time columns, use BETWEEN or = as appropriate.
Example output:
{{
    ""query"": ""SELECT * FROM customer_support_tickets WHERE ticket_subject ILIKE @p1 AND ticket_status = @p2 LIMIT {dbTop}"",
    ""parameters"": [{{ ""p1"": ""%issue%"", ""type"": ""VARCHAR"" }}, {{ ""p2"": ""Open"", ""type"": ""VARCHAR"" }}]
}}
{{
    ""query"": ""SELECT COUNT(*) AS ticket_count FROM customer_support_tickets WHERE ticket_status = @p1"",
    ""parameters"": [{{ ""p1"": ""Pending Customer Response"", ""type"": ""VARCHAR"" }}]
}}
{{
    ""query"": ""SELECT ticket_status, COUNT(*) AS Recurrence FROM customer_support_tickets GROUP BY ticket_status"",
    ""parameters"": {{}}
}}
{{
    ""query"": ""SELECT ticket_status, COUNT(*) AS Recurrence FROM customer_support_tickets WHERE ticket_type = @p1 GROUP BY ticket_status"",
    ""parameters"": [{{ ""p1"": ""Refund request"", ""type"": ""VARCHAR"" }}]
}}
{{
    ""query"": ""SELECT * FROM customer_support_tickets WHERE resolution IS NOT NULL LIMIT {dbTop}"",
    ""parameters"": {{}}
}}
{{
    ""query"": ""SELECT * FROM customer_support_tickets WHERE first_response_time BETWEEN @p1 AND @p2"",
    ""parameters"": [{{ ""p1"": ""2024-05-01 00:00:00"", ""type"": ""TIMESTAMP"" }}, {{ ""p2"": ""2024-05-01 23:59:59"", ""type"": ""TIMESTAMP"" }}]
}}");
            sqlQueryChat.AddUserMessage(question);
            var sqlResult = await chat.GetChatMessageContentAsync(sqlQueryChat, cancellationToken: cancellationToken);
            var sqlJson = sqlResult.Content ?? throw new InvalidOperationException("Failed to generate SQL query");

            // Logging value
            _logger.LogInformation("sqlResult: {@SqlResult}", sqlResult);
            _logger.LogInformation("sqljson: {@SqlJson}", sqlJson);

            var sqlObject = JsonSerializer.Deserialize<JsonElement>(sqlJson);
            var sqlQuery = sqlObject.GetProperty("query").GetString() ?? throw new InvalidOperationException("Failed to get SQL query");
            var parametersJson = sqlObject.GetProperty("parameters");
            bool isEmptyParameters = parametersJson.ValueKind == JsonValueKind.Array && parametersJson.GetArrayLength() == 0;
            List<Dictionary<string, object>>? parameters = null;
            if (!isEmptyParameters) // Only deserialize if parametersJson is not an empty JSON object
            {
                parameters = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(parametersJson.GetRawText()) ?? throw new InvalidOperationException("Failed to get SQL parameters");
            }

            // Step 2: Validate SQL Query with basic security check
            // Check for incorrect table name or no SELECT statement
            if (!sqlQuery.Contains("customer_support_tickets") || !sqlQuery.Contains("SELECT", StringComparison.CurrentCultureIgnoreCase))
            {
                throw new InvalidOperationException("Generated SQL query is invalid or unsafe");
            }
            // Check for limit in SELECT statement
            if (sqlQuery.Contains("SELECT", StringComparison.CurrentCultureIgnoreCase) && !sqlQuery.Contains("LIMIT", StringComparison.CurrentCultureIgnoreCase))
            {
                throw new InvalidOperationException($"Row selection query must include LIMIT {dbTop}");
            }

            // Step 3: Query the database using PostgresTicketService
            var ticketResults = await _ticketService.QueryTicketAsync(sqlQuery, parameters);

            // Step 4: Format the database results
            if (ticketResults.Count == 0)
            {
                content = "No customer support tickets found.";
            }
            else
            {
                if (sqlQuery.Contains("COUNT(*)", StringComparison.CurrentCultureIgnoreCase) && !sqlQuery.Contains("GROUP BY", StringComparison.CurrentCultureIgnoreCase)) // Aggregate function count all only, no group by clause
                {
                    var row = ticketResults[0];
                    var count = row.Values.FirstOrDefault()?.ToString() ?? "0";
                    content = $"Count: {count}";
                }
                else if (sqlQuery.Contains("COUNT", StringComparison.CurrentCultureIgnoreCase) && sqlQuery.Contains("GROUP BY", StringComparison.CurrentCultureIgnoreCase)) // Aggregate function count all with group by clause
                {
                    content = string.Join("\r", ticketResults.Select(row =>
                    {
                        var countDetails = string.Join(", ", row.Select(kv => $"{kv.Key}: {kv.Value}"));
                        return $"Count: {countDetails}";
                    }));
                }
                else // Regular select combined with some aggregates
                {
                    content = string.Join("\r", ticketResults.Select(row =>
                    {
                        var ticketDetails = string.Join(", ", row.Select(kv => $"{kv.Key}: {kv.Value}"));
                        return $"Ticket: {ticketDetails}";
                    }));
                }
            }

            // Step 5: Add content to answerChat with the appropriate prompt
            var prompt = @$"## Source ##
{content}
## End ##

Answer the question based on the available source.
Your answer needs to be a JSON object with the following format:
{{
    ""answer"": // The answer to the question. For database queries, summarize ticket details. For document queries, include source references [title]. If no source, answer as 'I don't know'.
    ""thoughts"": // Brief thoughts on how you came up with the answer, e.g., what sources or tickets you used.
}}";
            answerChat.AddUserMessage(prompt);
        }
        else // QueryMode.Document
        {
            if (overrides?.RetrievalMode != RetrievalMode.Text && embedding is not null)
            {
                embeddings = (await embedding.GenerateEmbeddingAsync(question, cancellationToken: cancellationToken)).ToArray();
            }

            // step 1
            // use llm to get query if retrieval mode is not vector
            string? query = null;
            if (overrides?.RetrievalMode != RetrievalMode.Vector)
            {
                var getQueryChat = new ChatHistory(@"You are a helpful AI assistant, generate search query for followup question.
Make your respond simple and precise. Return the query only, do not return any other text.
e.g.
Northwind Health Plus AND standard plan.
standard plan AND dental AND employee benefit.
");

                getQueryChat.AddUserMessage(question);
                var result = await chat.GetChatMessageContentAsync(
                    getQueryChat,
                    cancellationToken: cancellationToken);

                query = result.Content ?? throw new InvalidOperationException("Failed to get search query");
            }

            // step 2
            // use query to search related docs
            documentContentList = await _searchClient.QueryDocumentsAsync(query, embeddings, overrides, cancellationToken);
            content = documentContentList.Length == 0
                ? "no source available."
                : string.Join("\r", documentContentList.Select(x => $"{x.Title}:{x.Content}"));

            // step 2.5
            // retrieve images if _visionService is available
            if (_visionService is not null)
            {
                var queryEmbeddings = await _visionService.VectorizeTextAsync(query ?? question, cancellationToken);
                images = await _searchClient.QueryImagesAsync(query, queryEmbeddings.vector, overrides, cancellationToken);
            }

            if (images != null)
            {
                var prompt = @$"## Source ##
{content}
## End ##

Answer question based on available source and images.
Your answer needs to be a json object with answer and thoughts field.
Don't put your answer between ```json and ```, return the json string directly. e.g {{""answer"": ""I don't know"", ""thoughts"": ""I don't know""}}";
                var tokenRequestContext = new TokenRequestContext(new[] { "https://storage.azure.com/.default" });
                var sasToken = await (_tokenCredential?.GetTokenAsync(tokenRequestContext, cancellationToken) ?? throw new InvalidOperationException("Failed to get token"));
                var sasTokenString = sasToken.Token;
                var imageUrls = images.Select(x => $"{x.Url}?{sasTokenString}").ToArray();
                contentCollection = [new TextContent(prompt)];
                foreach (var imageUrl in imageUrls)
                {
                    contentCollection.Add(new ImageContent(new Uri(imageUrl)));
                }
                content = null; // Handled by collection

                // Add contentCollection to answerChat
                answerChat.AddUserMessage(contentCollection);
            }
            else
            {
                var prompt = @$"## Source ##
{content}
## End ##

Answer the question based on the available source.
Your answer needs to be a JSON object with the following format:
{{
    ""answer"": // The answer to the question. For database queries, summarize ticket details. For document queries, include source references [title]. If no source, answer as 'I don't know'.
    ""thoughts"": // Brief thoughts on how you came up with the answer, e.g., what sources or tickets you used.
}}";
                answerChat.AddUserMessage(prompt);
            }
        }

        var promptExecutingSetting = new OpenAIPromptExecutionSettings
        {
            MaxTokens = 1024,
            Temperature = overrides?.Temperature ?? 0.7,
            StopSequences = [],
        };

        // Step 6: get answer
        var answer = await chat.GetChatMessageContentAsync(
                       answerChat,
                       promptExecutingSetting,
                       cancellationToken: cancellationToken);
        var answerJson = answer.Content ?? throw new InvalidOperationException("Failed to get search query");
        var answerObject = JsonSerializer.Deserialize<JsonElement>(answerJson);
        var answerProperty = answerObject.GetProperty("answer");
        string ans;

        // Check for answer property variation
        if (answerProperty.ValueKind == JsonValueKind.Array) // Answer is in array mode
        {
            var tickets = answerProperty.EnumerateArray().Select(ticket =>
                string.Join(", ", ticket.EnumerateObject().Select(kv => $"{kv.Name}: {kv.Value}"))
            );
            ans = string.Join(", ", tickets);
        }
        else // Answer is a regular string
        {
            ans = answerProperty.GetString() ?? throw new InvalidOperationException("Failed to get answer");
        }
        var thoughts = answerObject.GetProperty("thoughts").GetString() ?? throw new InvalidOperationException("Failed to get thoughts");

        // step 7
        // add follow up questions if requested
        if (overrides?.SuggestFollowupQuestions is true)
        {
            var followUpQuestionChat = new ChatHistory(queryMode == QueryMode.Database
                ? @"You are a helpful AI assistant specializing in customer support ticket queries."
                : @"You are a helpful AI assistant");

            // Set follow up questions based on query mode
            string followUpPrompt;
            if (queryMode == QueryMode.Database)
            {
                followUpPrompt = $@"Generate three follow-up questions based on the answer you just generated about customer support tickets.
The questions should be relevant to the ticket context (e.g., ticket status, priority, customer details, product purchased, or resolution). Don't put your answer between ```json and ```, return the json string directly.
# Answer
{ans}

# Format of the response
Return the follow-up questions as a JSON string list.
e.g.
[
    ""What is the current status of the ticket?"",
    ""Can you provide more details about the resolution?"",
    ""How many tickets are open for this product?""
]";
            }
            else // QueryMode.Document
            {
                followUpPrompt = $@"Generate three follow-up question based on the answer you just generated.
# Answer
{ans}

# Format of the response
Return the follow-up question as a json string list. Don't put your answer between ```json and ```, return the json string directly.
e.g.
[
    ""What is the deductible?"",
    ""What is the co-pay?"",
    ""What is the out-of-pocket maximum?""
]";
            }
            followUpQuestionChat.AddUserMessage(followUpPrompt);

            var followUpQuestions = await chat.GetChatMessageContentAsync(
                followUpQuestionChat,
                promptExecutingSetting,
                cancellationToken: cancellationToken);

            var followUpQuestionsJson = followUpQuestions.Content ?? throw new InvalidOperationException("Failed to get search query");
            var followUpQuestionsObject = JsonSerializer.Deserialize<JsonElement>(followUpQuestionsJson);
            var followUpQuestionsList = followUpQuestionsObject.EnumerateArray().Select(x => x.GetString()!).ToList();
            foreach (var followUpQuestion in followUpQuestionsList)
            {
                ans += $" <<{followUpQuestion}>> ";
            }

            followUpQuestionList = followUpQuestionsList.ToArray();
        }

        var responseMessage = new ResponseMessage("assistant", ans);
        var responseContext = new ResponseContext(
            DataPointsContent: queryMode == QueryMode.Database
                ? Array.Empty<SupportingContentRecord>()
                : documentContentList?.Select(x => new SupportingContentRecord(x.Title, x.Content)).ToArray(),
            DataPointsImages: queryMode == QueryMode.Database
                ? Array.Empty<SupportingImageRecord>()
                : images?.Select(x => new SupportingImageRecord(x.Title, x.Url)).ToArray(),
            FollowupQuestions: followUpQuestionList ?? Array.Empty<string>(),
            Thoughts: new[] { new Thoughts("Thoughts", thoughts) });

        var choice = new ResponseChoice(
            Index: 0,
            Message: responseMessage,
            Context: responseContext,
            CitationBaseUrl: _configuration.ToCitationBaseUrl());

        return new ChatAppResponse(new[] { choice });
    }
}
