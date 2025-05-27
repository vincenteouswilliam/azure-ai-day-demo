// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using Azure.Core;
using Markdig.Helpers;
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
    private readonly PostgresDBService _dbService;
    private readonly EmailNotificationService _emailService;

    public ReadRetrieveReadChatService(
        ISearchService searchClient,
        OpenAIClient client,
        IConfiguration configuration,
        PostgresDBService dbService,
        EmailNotificationService emailService,
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
        _dbService = dbService;
        _emailService = emailService;
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
            : throw new InvalidOperationException("User question is null");

        string[]? followUpQuestionList = null;
        string? content = string.Empty;
        SupportingImageRecord[]? images = default;
        SupportingContentRecord[]? documentContentList = null;
        ChatMessageContentItemCollection? contentCollection = null;

        // step 0
        // put together related docs and conversation history to generate answer
        var answerChat = new ChatHistory(
            queryMode == QueryMode.Database
                ? "You are a system assistant who helps with accounts receivable queries. Be brief and precise in your answers."
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
            var sqlQueryChat = new ChatHistory($@"You are an expert AI assistant that generates SQL queries for a PostgreSQL database and handles notification requests for an accounts receivable database. Your task is to interpret user prompts, generate accurate, efficient, and secure parameterized SQL queries, and identify if the prompt includes a request to send an email notification (e.g., 'send notification', 'notify', 'email'). The database includes the following tables and schemas:

1. Clients:
   - ClientID (INTEGER, PRIMARY KEY, SERIAL) NOT NULL
   - ClientName (VARCHAR(100)) NOT NULL
   - ContactPerson (VARCHAR(100))
   - Email (VARCHAR(100))
   - Phone (VARCHAR(20))
   - Address (TEXT)
   - CreatedAt (TIMESTAMP WITH TIME ZONE) DEFAULT CURRENT_TIMESTAMP

2. Invoices:
   - InvoiceID (INTEGER, PRIMARY KEY, SERIAL) NOT NULL
   - ClientID (INTEGER, FOREIGN KEY REFERENCES Clients(ClientID)) NOT NULL
   - InvoiceNumber (VARCHAR(50), UNIQUE) NOT NULL
   - IssueDate (DATE) NOT NULL
   - DueDate (DATE) NOT NULL
   - TotalAmount (NUMERIC(15, 2)) NOT NULL
   - AmountPaid (NUMERIC(15, 2)) DEFAULT 0.00
   - Status (VARCHAR(20)) NOT NULL DEFAULT 'Open' CHECK (Status IN ('Open', 'Partially Paid', 'Paid', 'Overdue'))
   - CreatedAt (TIMESTAMP WITH TIME ZONE) DEFAULT CURRENT_TIMESTAMP

3. Payments:
   - PaymentID (INTEGER, PRIMARY KEY, SERIAL) NOT NULL
   - InvoiceID (INTEGER, FOREIGN KEY REFERENCES Invoices(InvoiceID)) NOT NULL
   - ClientID (INTEGER, FOREIGN KEY REFERENCES Clients(ClientID)) NOT NULL
   - PaymentDate (DATE) NOT NULL
   - Amount (NUMERIC(15, 2)) NOT NULL
   - PaymentMethod (VARCHAR(20)) NOT NULL CHECK (PaymentMethod IN ('Bank Transfer', 'Credit Card', 'Cash', 'Check', 'Other'))
   - ReferenceNumber (VARCHAR(50))
   - Notes (TEXT)
   - CreatedAt (TIMESTAMP WITH TIME ZONE) DEFAULT CURRENT_TIMESTAMP

4. InvoiceItems:
   - ItemID (INTEGER, PRIMARY KEY, SERIAL) NOT NULL
   - InvoiceID (INTEGER, FOREIGN KEY REFERENCES Invoices(InvoiceID)) NOT NULL
   - Description (VARCHAR(255)) NOT NULL
   - Quantity (INTEGER) NOT NULL
   - UnitPrice (NUMERIC(15, 2)) NOT NULL
   - TotalPrice (NUMERIC(15, 2), GENERATED ALWAYS AS (Quantity * UnitPrice)) STORED

Guidelines:
1. Query Generation:
   - Generate parameterized SQL queries using placeholders (e.g., @p1, @p2) compatible with C# and PostgreSQL.
   - Use ILIKE for text-based searches on columns like ClientName, ContactPerson, Email, Address (Clients), InvoiceNumber, Status (Invoices), ReferenceNumber, Notes (Payments), and Description (InvoiceItems).
   - For nullable columns (ContactPerson, Email, Phone, Address, ReferenceNumber, Notes), use IS NULL or IS NOT NULL when appropriate.
   - For date/time columns (IssueDate, DueDate, PaymentDate, CreatedAt), use BETWEEN or =, formatting parameter values as 'yyyy-MM-dd' for DATE and 'yyyy-MM-dd HH:mm:ss' for TIMESTAMP WITH TIME ZONE.
   - For numeric columns (TotalAmount, AmountPaid, Amount, Quantity, UnitPrice), use >=, <=, =, or BETWEEN for comparisons.
   - Include joins to combine data across tables (e.g., Clients with Invoices for client details).
   - Limit results to {dbTop} unless specified otherwise.
   - Ensure queries are syntactically correct, optimized (e.g., use indexes), and safe from SQL injection.

2. Column Mapping:
   - Map user terms to columns:
     - Client terms (e.g., 'Acme Corp') search ClientName or ContactPerson (ILIKE).
     - Invoice terms (e.g., 'INV-0001', 'overdue') search InvoiceNumber or Status.
     - Payment terms (e.g., 'bank transfer') search PaymentMethod or ReferenceNumber.
     - Item terms (e.g., 'Consulting Services') search Description in InvoiceItems.
     - Amount terms (e.g., 'over 5000') query TotalAmount, AmountPaid, or calculate (TotalAmount - AmountPaid).
     - Date terms (e.g., 'due in May 2025') query IssueDate, DueDate, or PaymentDate.
   - For financial queries (e.g., 'outstanding invoices'), calculate outstanding amounts and filter by Status ('Open', 'Partially Paid', 'Overdue').

3. Notification Handling:
   - If the prompt includes a notification request (e.g., 'send notification', 'notify', 'email'), include a 'notification' field in the output.
   - The 'notification' field is a dictionary with:
     - 'send': Boolean (true if notification is requested, false otherwise).
     - 'recipient_email': The client's Email from the Clients table (if available, else empty string).
     - 'subject': A brief subject line (e.g., 'Overdue Invoice Notification').
     - 'body': The email body, including relevant details (e.g., client name, invoice number, outstanding amount, due date).
   - Ensure the query retrieves the Email column from Clients when a notification is requested.
   - If multiple clients are involved, include only the first client's email or adjust based on context (e.g., client with most outstanding invoices).

4. Ambiguity Handling:
   - For ambiguous prompts, prioritize ClientName and InvoiceNumber for identifiers, and Notes or Description for details.
   - Combine multiple conditions (e.g., client name and overdue status) with AND/OR logically.
   - For aggregations (e.g., 'total unpaid amount'), use GROUP BY and SUM or COUNT, joining tables as needed.

5. Output Format:
   - Return a JSON object with:
     - 'query': The SQL query with placeholders.
     - 'parameters': A list of dictionaries with parameter names (e.g., p1), values, and data type (e.g., VARCHAR, INTEGER, DATE, TIMESTAMP, NUMERIC).
     - 'notification': A dictionary with 'send' (boolean, , use lowercase 'true' or 'false'), 'recipient_email' (string), 'subject' (string), and 'body' (string). If no notification is requested, set 'send' to false and leave other fields empty.
   - Format dates as 'yyyy-MM-dd' and datetimes as 'yyyy-MM-dd HH:mm:ss'.
   - If no parameters are needed, return an empty parameters list ({{}}).

Example Prompts and Outputs:
- Prompt: 'Show me top 5 overdue invoices'
  Output:
  {{
    ""query"": ""SELECT i.InvoiceNumber, c.ClientName, i.DueDate, i.TotalAmount, i.AmountPaid, (i.TotalAmount - i.AmountPaid) AS OutstandingAmount FROM Invoices i JOIN Clients c ON i.ClientID = c.ClientID WHERE i.Status = @p1 LIMIT 5"",
    ""parameters"": [{{""p1"": ""Overdue"", ""type"": ""VARCHAR""}}],
    ""notification"": {{""send"": false, ""recipient_email"": """", ""subject"": """", ""body"": """"}}
  }}
- Prompt: 'Show me the client name and email with the most outstanding invoices and send notification for overdue payment'
  Output:
  {{
    ""query"": ""SELECT c.ClientName, c.Email, SUM(i.TotalAmount - i.AmountPaid) AS TotalOutstanding FROM Invoices i JOIN Clients c ON i.ClientID = c.ClientID WHERE i.Status IN ('Open', 'Partially Paid', 'Overdue') GROUP BY c.ClientID, c.ClientName, c.Email ORDER BY TotalOutstanding DESC LIMIT 1"",
    ""parameters"": {{}},
    ""notification"": {{
      ""send"": true,
      ""recipient_email"": ""c.Email"",
      ""subject"": ""Overdue Invoice Notification"",
      ""body"": ""Dear [ClientName],\n\nYou have an outstanding balance of [TotalOutstanding] on your overdue invoices. Please make a payment at your earliest convenience.\n\nThank you,\nAccounts Receivable Team""
    }}
  }}
- Prompt: ""Count payments by payment method for invoices issued in 2024""
  Output:
  {{
        ""query"": ""SELECT p.PaymentMethod, COUNT(*) AS PaymentCount FROM Payments p JOIN Invoices i ON p.InvoiceID = i.InvoiceID WHERE i.IssueDate BETWEEN @p1 AND @p2 GROUP BY p.PaymentMethod"",
        ""parameters"": [{{""p1"": ""2024-01-01"", ""type"": ""DATE""}}, {{""p2"": ""2024-12-31"", ""type"": ""DATE""}}],
        ""notification"": {{""send"": false, ""recipient_email"": """", ""subject"": """", ""body"": """"}}
  }}
- Prompt: ""List invoices for Consulting Services""
  Output:
  {{
        ""query"": ""SELECT i.InvoiceNumber, c.ClientName, ii.Description, ii.Quantity, ii.UnitPrice, ii.TotalPrice FROM Invoices i JOIN Clients c ON i.ClientID = c.ClientID JOIN InvoiceItems ii ON i.InvoiceID = ii.InvoiceID WHERE ii.Description ILIKE @p1 LIMIT {dbTop}"",
        ""parameters"": [{{""p1"": ""%Consulting Services%"", ""type"": ""VARCHAR""}}],
        ""notification"": {{""send"": false, ""recipient_email"": """", ""subject"": """", ""body"": """"}}
  }}");
            sqlQueryChat.AddUserMessage(question);
            var sqlResult = await chat.GetChatMessageContentAsync(sqlQueryChat, cancellationToken: cancellationToken);
            var sqlJson = sqlResult.Content ?? throw new InvalidOperationException("Failed to generate SQL query");
            Console.WriteLine($"sqlJson: {sqlJson}");

            var sqlObject = JsonSerializer.Deserialize<JsonElement>(sqlJson);
            var sqlQuery = sqlObject.GetProperty("query").GetString() ?? throw new InvalidOperationException("Failed to get SQL query");
            var parametersJson = sqlObject.GetProperty("parameters");
            bool isEmptyParameters = (parametersJson.ValueKind == JsonValueKind.Array && parametersJson.GetArrayLength() == 0) || (parametersJson.ValueKind == JsonValueKind.Object && !parametersJson.EnumerateObject().Any());
            List<Dictionary<string, object>>? parameters = null;
            if (!isEmptyParameters) // Only deserialize if parametersJson is not an empty JSON object
            {
                parameters = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(parametersJson.GetRawText()) ?? throw new InvalidOperationException("Failed to get SQL parameters");
            }

            // Step 2: Validate SQL Query with basic security check
            // Check for incorrect table name or no SELECT statement
            if (!sqlQuery.Contains("SELECT", StringComparison.CurrentCultureIgnoreCase))
            {
                throw new InvalidOperationException("Generated SQL query is invalid or unsafe");
            }
            // Select only without count aggregate function and no limit clause is prohibited
            if (sqlQuery.Contains("SELECT", StringComparison.CurrentCultureIgnoreCase) && !sqlQuery.Contains("COUNT", StringComparison.CurrentCultureIgnoreCase) && !sqlQuery.Contains("SUM", StringComparison.CurrentCultureIgnoreCase) && !sqlQuery.Contains("LIMIT", StringComparison.CurrentCultureIgnoreCase))
            {
                throw new InvalidOperationException($"Row selection query must include LIMIT {dbTop}");
            }

            // Step 3: Query the database using PostgresDBService
            var dbResults = await _dbService.QueryDataAsync(sqlQuery, parameters);
            Console.WriteLine($"dbResults: {JsonSerializer.Serialize(dbResults)}");

            // Step 3.5: Handle notification if requested
            string notificationStatus = "";
            if (sqlObject.TryGetProperty("notification", out var notificationProp) && notificationProp.ValueKind == JsonValueKind.Object)
            {
                bool sendNotification = false;
                var sendProp = notificationProp.GetProperty("send");

                if (sendProp.ValueKind == JsonValueKind.True)
                {
                    sendNotification = true;
                }
                else if (sendProp.ValueKind == JsonValueKind.False)
                {
                    sendNotification = false;
                }
                else if (sendProp.ValueKind == JsonValueKind.String)
                {
                    var sendValue = sendProp.GetString()?.ToString();
                    sendNotification = sendValue == "true";
                }

                if (sendNotification)
                {
                    string recipientEmail = notificationProp.GetProperty("recipient_email").GetString() ?? "";
                    string subject = notificationProp.GetProperty("subject").GetString() ?? "";
                    string body = notificationProp.GetProperty("body").GetString() ?? "";

                    if (dbResults.Count > 0)
                    {
                        var firstRow = dbResults[0];
                        body = body.Replace("[ClientName]", firstRow.GetValueOrDefault("clientname")?.ToString() ?? "Client")
                                    .Replace("[TotalOutstanding]", firstRow.GetValueOrDefault("totaloutstanding")?.ToString() ?? "0.00");
                        recipientEmail = firstRow.GetValueOrDefault("email")?.ToString() ?? "";
                    }

                    Console.WriteLine($"After:\nrecipientEmail: {recipientEmail}\nsubject: {subject}\nbody: {body}");
                    var (success, message) = await _emailService.SendEmailAsync(recipientEmail, subject, body);
                    notificationStatus = success ? "Notification sent successfully." : $"Failed to send notification: {message}";
                }
            }

            // Step 4: Format the database results
            if (dbResults.Count == 0)
            {
                content = "No accounts receivable data found.";
            }
            else
            {
                if (sqlQuery.Contains("COUNT(*)", StringComparison.CurrentCultureIgnoreCase) && !sqlQuery.Contains("GROUP BY", StringComparison.CurrentCultureIgnoreCase)) // Aggregate function count all only, no group by clause
                {
                    var row = dbResults[0];
                    var count = row.Values.FirstOrDefault()?.ToString() ?? "0";
                    content = $"Count: {count}";
                }
                else if (sqlQuery.Contains("COUNT", StringComparison.CurrentCultureIgnoreCase) && sqlQuery.Contains("GROUP BY", StringComparison.CurrentCultureIgnoreCase)) // Aggregate function count all with group by clause
                {
                    content = string.Join("\r", dbResults.Select(row =>
                    {
                        var countDetails = string.Join(", ", row.Select(kv => $"{kv.Key}: {kv.Value}"));
                        return $"Count: {countDetails}";
                    }));
                }
                else // Regular select combined with some aggregates
                {
                    content = string.Join("\r", dbResults.Select(row =>
                    {
                        var details = string.Join(", ", row.Select(kv => $"{kv.Key}: {kv.Value}"));
                        return $"Record: {details}";
                    }));
                }
            }

            // Step 5: Add content to answerChat with the appropriate prompt
            var prompt = @$"## Source ##
{content}
## End ##
## Notification Status
{notificationStatus}
## End ##

Answer the question based on the source availability and/or key-value combinations.
Your answer needs to be a JSON object with the following format:
{{
    ""introduction"": // Provide an introductory statement based on the user's prompt (e.g., if the user's prompt is 'Show top 5 overdue invoices', return 'Here are the top 5 overdue invoices:'). If the source is not available or contains only one key-value pair with the value 'empty', return an empty string ("""").
    ""answer"": // The answer to the question. If the source contains one or more records with multiple key-value pairs (e.g., 'Record: InvoiceNumber: INV-0001, ClientName: Acme Corp'), return a list of dictionaries where each dictionary represents a record with its key-value pairs (e.g., [{{""InvoiceNumber"": ""INV-0001"", ""ClientName"": ""Acme Corp""}}, ...]). If the source has a single key-value pair with the value 'empty' (e.g., 'Record: Notes: empty'), return a brief explanation string based on the key and user prompt (e.g., 'No notes have been provided for this payment'). If the source is not available (e.g., 'No accounts receivable data found'), return 'My apologies, I can''t find the information you requested'.
    ""notification"": // The notification status, if any (e.g., 'Email sent successfully' or 'Failed to send notification: ...'). If no notification was requested or attempted, return an empty string ("""").
    ""thoughts"": // Brief thoughts on how you came up with the answer, e.g., what tables or columns you used.
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
    ""answer"": // The answer to the question. For database queries, summarize record details. For document queries, include source references [title]. If no source, answer as 'I don't know'.
    ""thoughts"": // Brief thoughts on how you came up with the answer, e.g., what sources or data you used.
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
        Console.WriteLine($"answerJson: {answerJson}");
        var answerObject = JsonSerializer.Deserialize<JsonElement>(answerJson);
        var answerProperty = answerObject.GetProperty("answer");
        string ans;

        // Check for answer property variation
        if (answerProperty.ValueKind == JsonValueKind.Array) // Answer is in array mode
        {
            ans = ConstructHtmlTable(answerProperty);
        }
        else // Answer is a regular value
        {
            if (answerProperty.ValueKind == JsonValueKind.Number) // Answer is an integer or double
            {
                ans = answerProperty.TryGetInt32(out var ansInt) ? ansInt.ToString() : answerProperty.TryGetDouble(out var ansDouble) ? ansDouble.ToString() : throw new InvalidOperationException("Failed to get answer");
            }
            else // Answer is a regular string
            {
                ans = answerProperty.GetString() ?? throw new InvalidOperationException("Failed to get answer");
            }
        }
        var thoughts = answerObject.GetProperty("thoughts").GetString() ?? throw new InvalidOperationException("Failed to get thoughts");
        var introduction = queryMode == QueryMode.Database ? answerObject.GetProperty("introduction").GetString() : null;
        var notification = queryMode == QueryMode.Database ? answerObject.GetProperty("notification").GetString() : null;

        // Add introduction message before the answer if introduction message exists
        if (!string.IsNullOrEmpty(introduction))
        {
            ans = $"{introduction}<br>\n" + ans;
        }

        // Add notification message after the answer if notification message exists
        if (!string.IsNullOrEmpty(notification))
        {
            ans += $"<br>\n<b>Notification:</b> {notification}";
        }

        // step 7
        // add follow up questions if requested
        if (overrides?.SuggestFollowupQuestions is true)
        {
            var followUpQuestionChat = new ChatHistory(queryMode == QueryMode.Database
                ? @"You are a helpful AI assistant specializing in accounts receivable queries."
                : @"You are a helpful AI assistant");

            // Set follow up questions based on query mode
            string followUpPrompt;
            if (queryMode == QueryMode.Database)
            {
                followUpPrompt = $@"Generate three follow-up questions based on the answer you just generated about accounts receivable data.
The questions should be relevant to the financial context (e.g., invoice status, client details, payment methods, or outstanding balances). Don't put your answer between ```json and ```, return the json string directly.
# Answer
{ans}

# Format of the response
Return the follow-up questions as a JSON string list.
e.g.
[
    ""What is the status of the invoice?"",
    ""Can you provide details about the payment history?"",
    ""Which clients have overdue invoices?""
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

        Console.WriteLine($"responseMessage: {responseMessage.Content}");

        return new ChatAppResponse(new[] { choice });
    }

    // Helper method to construct html table strings
    private static string ConstructHtmlTable(JsonElement je)
    {
        // Starts with a single <table> opener
        string tableString = "<table class=\"answer-table\">";

        // Table header
        JsonElement firstEntry = je.EnumerateArray().FirstOrDefault();
        var keys = firstEntry.EnumerateObject().Select(prop => prop.Name);
        string headers = "<tr>" + string.Join("", keys.Select(key => $"<th>{key}</th>")) + "</tr>";
        tableString += headers;

        // Table Data
        StringBuilder dataRows = new StringBuilder();
        foreach (var data in je.EnumerateArray())
        {
            dataRows.Append("<tr>");
            foreach (var key in keys)
            {
                string value = data.TryGetProperty(key, out JsonElement val)
                    ? val.ToString()
                    : "";
                dataRows.Append($"<td>{value}</td>");
            }
            dataRows.Append("</tr>");
        }
        tableString += dataRows.ToString();

        // Close with </table> to complete the string
        tableString += "</table>";

        // Return the constructed table string
        return tableString;
    }
}
