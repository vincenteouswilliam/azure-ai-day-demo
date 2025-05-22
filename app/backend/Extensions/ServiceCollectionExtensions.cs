// Copyright (c) Microsoft. All rights reserved.

namespace MinimalApi.Extensions;

internal static class ServiceCollectionExtensions
{
    private static readonly DefaultAzureCredential s_azureCredential = new();

    internal static IServiceCollection AddAzureServices(this IServiceCollection services)
    {
        services.AddSingleton<BlobServiceClient>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var azureStorageAccountEndpoint = config["AzureStorageAccountEndpoint"];
            ArgumentNullException.ThrowIfNullOrEmpty(azureStorageAccountEndpoint);

            var blobServiceClient = new BlobServiceClient(
                new Uri(azureStorageAccountEndpoint), s_azureCredential);

            return blobServiceClient;
        });

        services.AddSingleton<BlobContainerClient>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var azureStorageContainer = config["AzureStorageContainer"];
            return sp.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(azureStorageContainer);
        });

        services.AddSingleton<ISearchService, AzureSearchService>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var azureSearchServiceEndpoint = config["AzureSearchServiceEndpoint"];
            ArgumentNullException.ThrowIfNullOrEmpty(azureSearchServiceEndpoint);

            var azureSearchIndex = config["AzureSearchIndex"];
            ArgumentNullException.ThrowIfNullOrEmpty(azureSearchIndex);

            var searchClient = new SearchClient(
                               new Uri(azureSearchServiceEndpoint), azureSearchIndex, s_azureCredential);

            return new AzureSearchService(searchClient);
        });

        services.AddSingleton<DocumentAnalysisClient>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var azureOpenAiServiceEndpoint = config["AzureOpenAiServiceEndpoint"] ?? throw new ArgumentNullException();

            var documentAnalysisClient = new DocumentAnalysisClient(
                new Uri(azureOpenAiServiceEndpoint), s_azureCredential);
            return documentAnalysisClient;
        });

        services.AddSingleton<OpenAIClient>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var useAOAI = config["UseAOAI"] == "true";
            if (useAOAI)
            {
                var azureOpenAiServiceEndpoint = config["AzureOpenAiServiceEndpoint"];
                ArgumentNullException.ThrowIfNullOrEmpty(azureOpenAiServiceEndpoint);

                var openAIClient = new OpenAIClient(new Uri(azureOpenAiServiceEndpoint), s_azureCredential);

                return openAIClient;
            }
            else
            {
                var openAIApiKey = config["OpenAIApiKey"];
                ArgumentNullException.ThrowIfNullOrEmpty(openAIApiKey);

                var openAIClient = new OpenAIClient(openAIApiKey);
                return openAIClient;
            }
        });

        services.AddSingleton<AzureBlobStorageService>();

        // Add service for PostgreSQL
        services.AddSingleton<PostgresTicketService>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();

            return new PostgresTicketService(config);
        });

        services.AddSingleton<ReadRetrieveReadChatService>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var useVision = config["UseVision"] == "true";
            var openAIClient = sp.GetRequiredService<OpenAIClient>();
            var searchClient = sp.GetRequiredService<ISearchService>();
            var postgreClient = sp.GetRequiredService<PostgresTicketService>();
            var logger = sp.GetRequiredService<ILogger<ReadRetrieveReadChatService>>();
            if (useVision)
            {
                var azureComputerVisionServiceEndpoint = config["AzureComputerVisionServiceEndpoint"];
                ArgumentNullException.ThrowIfNullOrEmpty(azureComputerVisionServiceEndpoint);
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();

                var visionService = new AzureComputerVisionService(httpClient, azureComputerVisionServiceEndpoint, s_azureCredential);
                return new ReadRetrieveReadChatService(logger, searchClient, openAIClient, config, postgreClient, visionService, s_azureCredential);
            }
            else
            {
                return new ReadRetrieveReadChatService(logger, searchClient, openAIClient, config, postgreClient, tokenCredential: s_azureCredential);
            }
        });

        return services;
    }

    internal static IServiceCollection AddCrossOriginResourceSharing(this IServiceCollection services)
    {
        services.AddCors(
            options =>
                options.AddDefaultPolicy(
                    policy =>
                        policy.AllowAnyOrigin()
                            .AllowAnyHeader()
                            .AllowAnyMethod()));

        return services;
    }
}
