targetScope = 'subscription'

@description('Name of the environment used to generate a short unique hash for resources.')
@minLength(1)
@maxLength(64)
param environmentName string

@description('Primary location for all resources')
@allowed([
  'centralus'
  'eastus2'
  'eastasia'
  'westus'
  'westeurope'
  'westus2'
  'australiaeast'
  'eastus'
  'francecentral'
  'japaneast'
  'nortcentralus'
  'swedencentral'
  'switzerlandnorth'
  'uksouth'
])
param location string
param tags string = ''

@description('Location for the OpenAI resource group')
@allowed([
  'canadaeast'
  'westus'
  'eastus'
  'eastus2'
  'francecentral'
  'swedencentral'
  'switzerlandnorth'
  'uksouth'
  'japaneast'
  'northcentralus'
  'australiaeast'
])
@metadata({
  azd: {
    type: 'location'
  }
})
param openAiResourceGroupLocation string

@description('Name of the chat GPT model. Default: gpt-35-turbo')
@allowed(['gpt-35-turbo', 'gpt-4', 'gpt-4o', 'gpt-4o-mini', 'gpt-35-turbo-16k', 'gpt-4-16k'])
param azureOpenAIChatGptModelName string = 'gpt-4o-mini'

@description('Name of the chat GPT model. Default: 0613 for gpt-35-turbo, or choose 2024-07-18 for gpt-4o-mini')
@allowed(['0613', '2024-07-18'])
param azureOpenAIChatGptModelVersion string = '2024-07-18'

@description('Defines if the process will deploy an Azure Application Insights resource')
param useApplicationInsights bool = true

// @description('Name of the Azure Application Insights dashboard')
param applicationInsightsDashboardName string = ''

@description('Name of the Azure Application Insights resource')
param applicationInsightsName string = ''

@description('Name of the Azure App Service Plan')
param appServicePlanName string = ''

@description('Capacity of the chat GPT deployment. Default: 10')
param chatGptDeploymentCapacity int = 8

@description('Name of the chat GPT deployment')
param azureChatGptDeploymentName string = 'chat'

@description('Name of the Azure Cognitive Services Computer Vision service')
param computerVisionServiceName string = ''

@description('Location of the resource group for the Azure Cognitive Services Computer Vision service')
param computerVisionResourceGroupLocation string = 'eastus' // Vision vectorize API is yet to be deployed globally

@description('SKU name for the Azure Cognitive Services Computer Vision service. Default: S1')
param computerVisionSkuName string = 'S1'

@description('Name of the embedding deployment. Default: embedding')
param azureEmbeddingDeploymentName string = 'embedding'

@description('Capacity of the embedding deployment. Default: 30')
param embeddingDeploymentCapacity int = 30

@description('Name of the embedding model. Default: text-embedding-ada-002')
param azureEmbeddingModelName string = 'text-embedding-ada-002'

@description('Name of the container apps environment')
param containerAppsEnvironmentName string = ''

@description('Name of the Azure container registry')
param containerRegistryName string = ''

@description('Name of the resource group for the Azure container registry')
param containerRegistryResourceGroupName string = ''

@description('Location of the resource group for the Azure AI Document Intelligence service')
param formRecognizerResourceGroupLocation string = location

@description('Name of the Azure AI Document Intelligence service')
param formRecognizerServiceName string = ''

@description('SKU name for the Azure AI Document Intelligence service. Default: S0')
@allowed(['S0', 'F0'])
param formRecognizerSkuName string = 'S0'

@description('Name of the Azure Function App')
param functionServiceName string = ''

@description('Name of the Azure Key Vault')
param keyVaultName string = ''

@description('Location of the resource group for the Azure Key Vault')
param keyVaultResourceGroupLocation string = location

@description('Name of the Azure Log Analytics workspace')
param logAnalyticsName string = ''

@description('Name of the OpenAI service')
param openAiServiceName string = ''

@description('SKU name for the OpenAI service. Default: S0')
param openAiSkuName string = 'S0'

@description('ID of the principal')
param principalId string = ''

@description('Type of the principal. Valid values: User,ServicePrincipal')
param principalType string = 'User'

@description('Name of the resource group')
param resourceGroupName string = ''

@description('Name of the search index. Default: gptkbindex')
param searchIndexName string = 'gptkbindex'

@description('Name of the Azure AI Search service')
param searchServiceName string = ''

@description('Location of the resource group for the Azure AI Search service')
param searchServiceResourceGroupLocation string = location

@description('Azure AI Search Semantic Ranker Level')
param searchServiceSemanticRankerLevel string // Set in main.parameters.json

@description('SKU name for the Azure AI Search service. Default: standard')
param searchServiceSkuName string = 'standard'

var actualSearchServiceSemanticRankerLevel = (searchServiceSkuName == 'free')
  ? 'disabled'
  : searchServiceSemanticRankerLevel

@description('Name of the storage account')
param storageAccountName string = ''

@description('Name of the storage container. Default: content')
param storageContainerName string = 'content'

@description('Location of the resource group for the storage account')
param storageResourceGroupLocation string = location

@description('Specifies if the web app exists')
param webAppExists bool = false

@description('Name of the web app container')
param webContainerAppName string = ''

@description('Name of the web app identity')
param webIdentityName string = ''

@description('Name of the web app image')
param webImageName string = ''

@description('Use Azure OpenAI service')
param useAOAI bool

@description('OpenAI API Key, leave empty to provision a new Azure OpenAI instance')
param openAIApiKey string

@description('OpenAI Model')
param openAiChatGptDeployment string

@description('OpenAI Embedding Model')
param openAiEmbeddingDeployment string

@description('Use Vision retrieval. default: false')
param useVision bool = false

// PostgresSQL parameters
@description('Name of the PostgreSQL server')
param postgresServerName string = ''

@description('Name of the PostgreSQL database')
param postgresDatabaseName string = ''

@description('Administrator username for postgre server')
param postgresAdministratorLogin string = ''

@description('Administrator password for postgre server')
@secure()
param postgresAdministratorLoginPassword string = newGuid()

@description('PostgreSQL SKU name')
param postgresSkuName string = ''

@description('PostgreSQL tier name')
param postgresTierName string = ''

@description('PostgreSQL server storage size in GB')
param postgresStorageSizeGB int = 32

@description('PostgreSQL server version')
@allowed(['11', '12', '13', '14', '15', '16'])
param postgresVersion string = '16'

// Mail related settings
@description('Mail SMTP Host')
param mailSmtpHost string = 'smtp.gmail.com'

@description('Mail SMTP Port')
param mailSmtpPort int = 587

@description('Mail Sender Email')
param mailSenderEmailAddress string

@description('Mail Sender Password')
@secure()
param mailSenderEmailPassword string

@description('Mail Sender Display Name')
param mailSenderDisplayName string = 'Account Receivable'

@description('Mail dummy recipient address to overwrite')
param mailDummyRecipientAddress string = ''

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

var baseTags = { 'azd-env-name': environmentName }
var updatedTags = union(empty(tags) ? {} : base64ToJson(tags), baseTags)

// Organize resources in a resource group
resource resourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: !empty(resourceGroupName) ? resourceGroupName : '${abbrs.resourcesResourceGroups}${environmentName}'
  location: location
  tags: updatedTags
}

// Store secrets in a keyvault
module keyVault 'core/security/keyvault.bicep' = {
  name: 'keyvault'
  scope: resourceGroup
  params: {
    name: !empty(keyVaultName) ? keyVaultName : '${abbrs.keyVaultVaults}${resourceToken}'
    location: keyVaultResourceGroupLocation
    tags: updatedTags
    principalId: principalId
  }
}

module keyVaultSecrets 'core/security/keyvault-secrets.bicep' = {
  scope: resourceGroup
  name: 'keyvault-secrets'
  params: {
    keyVaultName: keyVault.outputs.name
    tags: updatedTags
    secrets: concat(
      [
        {
          name: 'AzureSearchServiceEndpoint'
          value: searchService.outputs.endpoint
        }
        {
          name: 'AzureSearchIndex'
          value: searchIndexName
        }
        {
          name: 'AzureStorageAccountEndpoint'
          value: storage.outputs.primaryEndpoints.blob
        }
        {
          name: 'AzureStorageContainer'
          value: storageContainerName
        }
        {
          name: 'UseAOAI'
          value: useAOAI ? 'true' : 'false'
        }
        {
          name: 'UseVision'
          value: useVision ? 'true' : 'false'
        }
      ],
      useAOAI
        ? [
            {
              name: 'AzureOpenAiServiceEndpoint'
              value: azureOpenAi.outputs.endpoint
            }
            {
              name: 'AzureOpenAiChatGptDeployment'
              value: azureChatGptDeploymentName
            }
            {
              name: 'AzureOpenAiEmbeddingDeployment'
              value: azureEmbeddingDeploymentName
            }
          ]
        : [
            {
              name: 'OpenAIAPIKey'
              value: openAIApiKey
            }
            {
              name: 'OpenAiChatGptDeployment'
              value: openAiChatGptDeployment
            }
            {
              name: 'OpenAiEmbeddingDeployment'
              value: openAiEmbeddingDeployment
            }
          ],
      useVision
        ? [
            {
              name: 'AzureComputerVisionServiceEndpoint'
              value: computerVision.outputs.endpoint
            }
          ]
        : []
    )
  }
}

// Container apps host (including container registry)
module containerApps 'core/host/container-apps.bicep' = {
  name: 'container-apps'
  scope: resourceGroup
  params: {
    name: 'app'
    containerAppsEnvironmentName: !empty(containerAppsEnvironmentName)
      ? containerAppsEnvironmentName
      : '${abbrs.appManagedEnvironments}${resourceToken}'
    containerRegistryName: !empty(containerRegistryName)
      ? containerRegistryName
      : '${abbrs.containerRegistryRegistries}${resourceToken}'
    containerRegistryResourceGroupName: !empty(containerRegistryResourceGroupName)
      ? containerRegistryResourceGroupName
      : resourceGroup.name
    location: location
    logAnalyticsWorkspaceName: monitoring.outputs.logAnalyticsWorkspaceName
  }
}

// Web frontend
module web './app/web.bicep' = {
  name: 'web'
  scope: resourceGroup
  params: {
    name: !empty(webContainerAppName) ? webContainerAppName : '${abbrs.appContainerApps}web-${resourceToken}'
    location: location
    tags: updatedTags
    imageName: webImageName
    identityName: !empty(webIdentityName)
      ? webIdentityName
      : '${abbrs.managedIdentityUserAssignedIdentities}web-${resourceToken}'
    applicationInsightsName: monitoring.outputs.applicationInsightsName
    containerAppsEnvironmentName: containerApps.outputs.environmentName
    containerRegistryName: containerApps.outputs.registryName
    exists: webAppExists
    keyVaultName: keyVault.outputs.name
    keyVaultResourceGroupName: resourceGroup.name
    storageBlobEndpoint: storage.outputs.primaryEndpoints.blob
    storageContainerName: storageContainerName
    searchServiceEndpoint: searchService.outputs.endpoint
    searchIndexName: searchIndexName
    formRecognizerEndpoint: formRecognizer.outputs.endpoint
    computerVisionEndpoint: useVision ? computerVision.outputs.endpoint : ''
    useVision: useVision
    openAiApiKey: useAOAI ? '' : openAIApiKey
    openAiEndpoint: useAOAI ? azureOpenAi.outputs.endpoint : ''
    openAiChatGptDeployment: useAOAI ? azureChatGptDeploymentName : ''
    openAiEmbeddingDeployment: useAOAI ? azureEmbeddingDeploymentName : ''
    serviceBinds: []
    postgresConnectionString: postgres.outputs.postgresConnectionString
    mailSmtpHost: mailSmtpHost
    mailSmtpPort: mailSmtpPort
    mailSenderEmailAddress: mailSenderEmailAddress
    mailSenderEmailPassword: mailSenderEmailPassword
    mailSenderDisplayName: mailSenderDisplayName
    mailDummyRecipientAddress: mailDummyRecipientAddress
  }
}

// Create an App Service Plan to group applications under the same payment plan and SKU
module appServicePlan './core/host/appserviceplan.bicep' = {
  name: 'appserviceplan'
  scope: resourceGroup
  params: {
    name: !empty(appServicePlanName) ? appServicePlanName : '${abbrs.webServerFarms}${resourceToken}'
    location: location
    tags: updatedTags
    sku: {
      name: 'Y1'
      tier: 'Dynamic'
    }
  }
}

// The application backend
module function './app/function.bicep' = {
  name: 'function'
  scope: resourceGroup
  params: {
    name: !empty(functionServiceName) ? functionServiceName : '${abbrs.webSitesFunctions}function-${resourceToken}'
    location: location
    tags: updatedTags
    applicationInsightsName: monitoring.outputs.applicationInsightsName
    appServicePlanId: appServicePlan.outputs.id
    keyVaultName: keyVault.outputs.name
    storageAccountName: storage.outputs.name
    allowedOrigins: [web.outputs.SERVICE_WEB_URI]
    appSettings: {
      AZURE_FORMRECOGNIZER_SERVICE_ENDPOINT: formRecognizer.outputs.endpoint
      AZURE_SEARCH_SERVICE_ENDPOINT: searchService.outputs.endpoint
      AZURE_SEARCH_INDEX: searchIndexName
      AZURE_SEARCH_SEMANTIC_RANKER: actualSearchServiceSemanticRankerLevel
      AZURE_STORAGE_BLOB_ENDPOINT: storage.outputs.primaryEndpoints.blob
      AZURE_OPENAI_EMBEDDING_DEPLOYMENT: useAOAI ? azureEmbeddingDeploymentName : ''
      OPENAI_EMBEDDING_DEPLOYMENT: useAOAI ? '' : openAiEmbeddingDeployment
      AZURE_OPENAI_ENDPOINT: useAOAI ? azureOpenAi.outputs.endpoint : ''
      USE_VISION: string(useVision)
      USE_AOAI: string(useAOAI)
      AZURE_COMPUTER_VISION_ENDPOINT: useVision ? computerVision.outputs.endpoint : ''
      OPENAI_API_KEY: useAOAI ? '' : openAIApiKey
      AZURE_POSTGRESQL_CONNECTION_STRING: postgres.outputs.postgresConnectionString
      MAIL_SMTP_HOST: mailSmtpHost
      MAIL_SMTP_PORT: mailSmtpPort
      MAIL_SENDER_EMAIL_ADDRESS: mailSenderEmailAddress
      MAIL_SENDER_EMAIL_PASSWORD: mailSenderEmailPassword
      MAIL_SENDER_DISPLAY_NAME: mailSenderDisplayName
      MAIL_DUMMY_RECIPIENT_ADDRESS: mailDummyRecipientAddress
    }
  }
}

// Monitor application with Azure Monitor
module monitoring 'core/monitor/monitoring.bicep' = {
  name: 'monitoring'
  scope: resourceGroup
  params: {
    location: location
    tags: updatedTags
    includeApplicationInsights: true
    logAnalyticsName: !empty(logAnalyticsName)
      ? logAnalyticsName
      : '${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    applicationInsightsName: !empty(applicationInsightsName)
      ? applicationInsightsName
      : '${abbrs.insightsComponents}${resourceToken}'
    applicationInsightsDashboardName: !empty(applicationInsightsDashboardName)
      ? applicationInsightsDashboardName
      : '${abbrs.portalDashboards}${resourceToken}'
  }
}

module azureOpenAi 'core/ai/cognitiveservices.bicep' = if (useAOAI) {
  name: 'openai'
  scope: resourceGroup
  params: {
    name: !empty(openAiServiceName) ? openAiServiceName : '${abbrs.cognitiveServicesAccounts}${resourceToken}'
    location: openAiResourceGroupLocation
    tags: updatedTags
    sku: {
      name: openAiSkuName
    }
    deployments: concat(
      [
        {
          name: azureEmbeddingDeploymentName
          model: {
            format: 'OpenAI'
            name: azureEmbeddingModelName
            version: '2'
          }
          sku: {
            name: 'Standard'
            capacity: embeddingDeploymentCapacity
          }
        }
      ],
      useVision
        ? [
            {
              name: azureChatGptDeploymentName
              model: {
                format: 'OpenAI'
                name: azureOpenAIChatGptModelName
                version: '2024-05-13'
              }
              sku: {
                name: 'Standard'
                capacity: chatGptDeploymentCapacity
              }
            }
          ]
        : [
            {
              name: azureChatGptDeploymentName
              model: {
                format: 'OpenAI'
                name: azureOpenAIChatGptModelName
                version: azureOpenAIChatGptModelVersion
              }
              sku: {
                name: 'Standard'
                capacity: chatGptDeploymentCapacity
              }
            }
          ]
    )
  }
}

// create computer vision for image embedding && text embedding api
module computerVision 'core/ai/cognitiveservices.bicep' = if (useVision) {
  name: 'computerVision'
  scope: resourceGroup
  params: {
    name: !empty(computerVisionServiceName)
      ? computerVisionServiceName
      : '${abbrs.cognitiveServicesComputerVision}${resourceToken}'
    kind: 'ComputerVision'
    location: computerVisionResourceGroupLocation
    tags: updatedTags
    sku: {
      name: computerVisionSkuName
    }
  }
}

module formRecognizer 'core/ai/cognitiveservices.bicep' = {
  name: 'formrecognizer'
  scope: resourceGroup
  params: {
    name: !empty(formRecognizerServiceName)
      ? formRecognizerServiceName
      : '${abbrs.cognitiveServicesFormRecognizer}${resourceToken}'
    kind: 'FormRecognizer'
    location: formRecognizerResourceGroupLocation
    tags: updatedTags
    sku: {
      name: formRecognizerSkuName
    }
  }
}

module searchService 'core/search/search-services.bicep' = {
  name: 'search-service'
  scope: resourceGroup
  params: {
    name: !empty(searchServiceName) ? searchServiceName : 'gptkb-${resourceToken}'
    location: searchServiceResourceGroupLocation
    tags: updatedTags
    authOptions: {
      aadOrApiKey: {
        aadAuthFailureMode: 'http401WithBearerChallenge'
      }
    }
    sku: {
      name: searchServiceSkuName
    }
    semanticSearch: actualSearchServiceSemanticRankerLevel //semanticSearch: 'free'
  }
}

module storage 'core/storage/storage-account.bicep' = {
  name: 'storage'
  scope: resourceGroup
  params: {
    name: !empty(storageAccountName) ? storageAccountName : '${abbrs.storageStorageAccounts}${resourceToken}'
    location: storageResourceGroupLocation
    tags: updatedTags
    publicNetworkAccess: 'Enabled'
    sku: {
      name: 'Standard_LRS'
    }
    deleteRetentionPolicy: {
      enabled: true
      days: 2
    }
    containers: [
      {
        name: storageContainerName
        publicAccess: 'Blob'
      }
    ]
  }
}

// PostgreSQL Module
module postgres 'core/database/postgresql.bicep' = {
  name: 'postgres'
  scope: resourceGroup
  params: {
    postgresServerName: !empty(postgresServerName) ? postgresServerName : '${abbrs.postgreSqlServers}${resourceToken}'
    location: location
    tags: updatedTags
    administratorLogin: postgresAdministratorLogin
    administratorLoginPassword: postgresAdministratorLoginPassword
    postgresDatabaseName: startsWith(postgresDatabaseName, abbrs.postgreSqlDatabases)
      ? postgresDatabaseName
      : '${abbrs.postgreSqlDatabases}${postgresDatabaseName}'
    skuName: postgresSkuName
    tier: postgresTierName
    storageSizeGB: postgresStorageSizeGB
    version: postgresVersion
  }
}

// USER ROLES
module azureOpenAiRoleUser 'core/security/role.bicep' = if (useAOAI) {
  scope: resourceGroup
  name: 'openai-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
    principalType: principalType
  }
}

module formRecognizerRoleUser 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'formrecognizer-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: 'a97b65f3-24c7-4388-baec-2e87135dc908'
    principalType: principalType
  }
}

module storageRoleUser 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'storage-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
    principalType: principalType
  }
}

module storageContribRoleUser 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'storage-contribrole-user'
  params: {
    principalId: principalId
    roleDefinitionId: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
    principalType: principalType
  }
}

module searchRoleUser 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'search-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: '1407120a-92aa-4202-b7e9-c0e197c71c8f'
    principalType: principalType
  }
}

module searchContribRoleUser 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'search-contrib-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
    principalType: principalType
  }
}

module searchSvcContribRoleUser 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'search-svccontrib-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: '7ca78c08-252a-4471-8644-bb5ff32d4ba0'
    principalType: principalType
  }
}

module visionRoleUser 'core/security/role.bicep' = if (useVision) {
  scope: resourceGroup
  name: 'vision-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: 'a97b65f3-24c7-4388-baec-2e87135dc908'
    principalType: principalType
  }
}

// FUNCTION ROLES
module AzureOpenAiRoleFunction 'core/security/role.bicep' = if (useAOAI) {
  scope: resourceGroup
  name: 'openai-role-function'
  params: {
    principalId: function.outputs.SERVICE_FUNCTION_IDENTITY_PRINCIPAL_ID
    roleDefinitionId: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
    principalType: 'ServicePrincipal'
  }
}

module formRecognizerRoleFunction 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'formrecognizer-role-function'
  params: {
    principalId: function.outputs.SERVICE_FUNCTION_IDENTITY_PRINCIPAL_ID
    roleDefinitionId: 'a97b65f3-24c7-4388-baec-2e87135dc908'
    principalType: 'ServicePrincipal'
  }
}

module storageRoleFunction 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'storage-role-function'
  params: {
    principalId: function.outputs.SERVICE_FUNCTION_IDENTITY_PRINCIPAL_ID
    roleDefinitionId: '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
    principalType: 'ServicePrincipal'
  }
}

module storageContribRoleFunction 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'storage-contribrole-function'
  params: {
    principalId: function.outputs.SERVICE_FUNCTION_IDENTITY_PRINCIPAL_ID
    roleDefinitionId: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
    principalType: 'ServicePrincipal'
  }
}

module searchRoleFunction 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'search-role-function'
  params: {
    principalId: function.outputs.SERVICE_FUNCTION_IDENTITY_PRINCIPAL_ID
    roleDefinitionId: '1407120a-92aa-4202-b7e9-c0e197c71c8f'
    principalType: 'ServicePrincipal'
  }
}

module searchContribRoleFunction 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'search-contrib-role-function'
  params: {
    principalId: function.outputs.SERVICE_FUNCTION_IDENTITY_PRINCIPAL_ID
    roleDefinitionId: '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
    principalType: 'ServicePrincipal'
  }
}

module searchSvcContribRoleFunction 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'search-svccontrib-role-function'
  params: {
    principalId: function.outputs.SERVICE_FUNCTION_IDENTITY_PRINCIPAL_ID
    roleDefinitionId: '7ca78c08-252a-4471-8644-bb5ff32d4ba0'
    principalType: 'ServicePrincipal'
  }
}

module visionRoleFunction 'core/security/role.bicep' = if (useVision) {
  scope: resourceGroup
  name: 'vision-role-function'
  params: {
    principalId: function.outputs.SERVICE_FUNCTION_IDENTITY_PRINCIPAL_ID
    roleDefinitionId: 'a97b65f3-24c7-4388-baec-2e87135dc908'
    principalType: 'ServicePrincipal'
  }
}

// SYSTEM IDENTITIES
module azureOpenAiRoleBackend 'core/security/role.bicep' = if (useAOAI) {
  scope: resourceGroup
  name: 'openai-role-backend'
  params: {
    principalId: web.outputs.SERVICE_WEB_IDENTITY_PRINCIPAL_ID
    roleDefinitionId: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
    principalType: 'ServicePrincipal'
  }
}

module storageRoleBackend 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'storage-role-backend'
  params: {
    principalId: web.outputs.SERVICE_WEB_IDENTITY_PRINCIPAL_ID
    roleDefinitionId: '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
    principalType: 'ServicePrincipal'
  }
}

module storageContribRoleBackend 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'storage-contribrole-backend'
  params: {
    principalId: web.outputs.SERVICE_WEB_IDENTITY_PRINCIPAL_ID
    roleDefinitionId: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
    principalType: 'ServicePrincipal'
  }
}

module searchRoleBackend 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'search-role-backend'
  params: {
    principalId: web.outputs.SERVICE_WEB_IDENTITY_PRINCIPAL_ID
    roleDefinitionId: '1407120a-92aa-4202-b7e9-c0e197c71c8f'
    principalType: 'ServicePrincipal'
  }
}

module visionRoleBackend 'core/security/role.bicep' = if (useVision) {
  scope: resourceGroup
  name: 'vision-role-backend'
  params: {
    principalId: web.outputs.SERVICE_WEB_IDENTITY_PRINCIPAL_ID
    roleDefinitionId: 'a97b65f3-24c7-4388-baec-2e87135dc908'
    principalType: 'ServicePrincipal'
  }
}

output APPLICATIONINSIGHTS_CONNECTION_STRING string = monitoring.outputs.applicationInsightsConnectionString
output APPLICATIONINSIGHTS_NAME string = monitoring.outputs.applicationInsightsName
output AZURE_USE_APPLICATION_INSIGHTS bool = useApplicationInsights
output AZURE_CONTAINER_ENVIRONMENT_NAME string = containerApps.outputs.environmentName
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerApps.outputs.registryLoginServer
output AZURE_CONTAINER_REGISTRY_NAME string = containerApps.outputs.registryName
output AZURE_CONTAINER_REGISTRY_RESOURCE_GROUP string = containerApps.outputs.registryName
output AZURE_FORMRECOGNIZER_RESOURCE_GROUP string = resourceGroup.name
output AZURE_FORMRECOGNIZER_SERVICE string = formRecognizer.outputs.name
output AZURE_FORMRECOGNIZER_SERVICE_ENDPOINT string = formRecognizer.outputs.endpoint
output AZURE_COMPUTERVISION_RESOURCE_GROUP string = useVision ? resourceGroup.name : ''
output AZURE_COMPUTERVISION_SERVICE string = useVision ? computerVision.outputs.name : ''
output AZURE_COMPUTERVISION_SERVICE_ENDPOINT string = useVision ? computerVision.outputs.endpoint : ''
output AZURE_KEY_VAULT_ENDPOINT string = keyVault.outputs.endpoint
output AZURE_KEY_VAULT_NAME string = keyVault.outputs.name
output AZURE_KEY_VAULT_RESOURCE_GROUP string = resourceGroup.name
output AZURE_LOCATION string = location
output AZURE_OPENAI_RESOURCE_LOCATION string = openAiResourceGroupLocation
output AZURE_OPENAI_CHATGPT_DEPLOYMENT string = azureChatGptDeploymentName
output AZURE_OPENAI_EMBEDDING_DEPLOYMENT string = azureEmbeddingDeploymentName
output AZURE_OPENAI_ENDPOINT string = useAOAI ? azureOpenAi.outputs.endpoint : ''
output AZURE_OPENAI_RESOURCE_GROUP string = useAOAI ? resourceGroup.name : ''
output AZURE_OPENAI_SERVICE string = useAOAI ? azureOpenAi.outputs.name : ''
output AZURE_RESOURCE_GROUP string = resourceGroup.name
output AZURE_SEARCH_INDEX string = searchIndexName
output AZURE_SEARCH_SERVICE string = searchService.outputs.name
output AZURE_SEARCH_SERVICE_ENDPOINT string = searchService.outputs.endpoint
output AZURE_SEARCH_SERVICE_RESOURCE_GROUP string = resourceGroup.name
output AZURE_SEARCH_SERVICE_SKU string = searchServiceSkuName
output AZURE_STORAGE_ACCOUNT string = storage.outputs.name
output AZURE_STORAGE_BLOB_ENDPOINT string = storage.outputs.primaryEndpoints.blob
output AZURE_STORAGE_CONTAINER string = storageContainerName
output AZURE_STORAGE_RESOURCE_GROUP string = resourceGroup.name
output AZURE_TENANT_ID string = tenant().tenantId
output SERVICE_WEB_IDENTITY_NAME string = web.outputs.SERVICE_WEB_IDENTITY_NAME
output SERVICE_WEB_NAME string = web.outputs.SERVICE_WEB_NAME
output SERVICE_FUNCTION_IDENTITY_PRINCIPAL_ID string = function.outputs.SERVICE_FUNCTION_IDENTITY_PRINCIPAL_ID
output USE_AOAI bool = useAOAI
output USE_VISION bool = useVision
output OPENAI_EMBEDDING_DEPLOYMENT string = openAiEmbeddingDeployment
output AZURE_OPENAI_CHATGPT_MODEL_VERSION string = azureOpenAIChatGptModelVersion
output AZURE_OPENAI_CHATGPT_MODEL_NAME string = azureOpenAIChatGptModelName

// PostgreSQL outputs
output AZURE_POSTGRESQL_RESOURCE_GROUP string = resourceGroup.name
output AZURE_POSTGRESQL_SERVER_NAME string = postgres.outputs.postgresServerName
output AZURE_POSTGRESQL_DATABASE_NAME string = postgres.outputs.postgresDatabaseName
output AZURE_POSTGRESQL_CONNECTION_STRING string = postgres.outputs.postgresConnectionString
output AZURE_POSTGRESQL_LOCAL_CONN_STRING string = postgres.outputs.postgresLocalConnectionString
output AZURE_POSTGRESQL_ADMINISTRATOR_LOGIN_PASSWORD string = postgres.outputs.postgresAdministratorLoginPassword

// Mail outputs
output MAIL_SMTP_HOST string = mailSmtpHost
output MAIL_SMTP_PORT int = mailSmtpPort
output MAIL_SENDER_DISPLAY_NAME string = mailSenderDisplayName
output MAIL_DUMMY_RECIPIENT_ADDRESS string = mailDummyRecipientAddress
