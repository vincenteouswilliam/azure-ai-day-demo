param name string
param location string = resourceGroup().location
param tags object = {}

@description('The name of the identity')
param identityName string

@description('The name of the Application Insights')
param applicationInsightsName string

@description('The name of the container apps environment')
param containerAppsEnvironmentName string

@description('The name of the container registry')
param containerRegistryName string

@description('The name of the service')
param serviceName string = 'web'

@description('The name of the image')
param imageName string = ''

@description('Specifies if the resource exists')
param exists bool

@description('The name of the Key Vault')
param keyVaultName string

@description('The name of the Key Vault resource group')
param keyVaultResourceGroupName string = resourceGroup().name

@description('The storage blob endpoint')
param storageBlobEndpoint string

@description('The name of the storage container')
param storageContainerName string

@description('The search service endpoint')
param searchServiceEndpoint string

@description('The search index name')
param searchIndexName string

@description('The Azure AI Document Intelligence endpoint')
param formRecognizerEndpoint string

@description('The Computer Vision endpoint')
param computerVisionEndpoint string

@description('The OpenAI endpoint')
param openAiEndpoint string

@description('The OpenAI ChatGPT deployment name')
param openAiChatGptDeployment string

@description('The OpenAI Embedding deployment name')
param openAiEmbeddingDeployment string

@description('use gpt-4v')
param useVision bool = false

@description('The OpenAI API key')
param openAiApiKey string

@description('An array of service binds')
param serviceBinds array

@description('PostgreSQL connection string')
param postgresConnectionString string

// Mail related settings
@description('Mail SMTP Host')
param mailSmtpHost string

@description('Mail SMTP Port')
param mailSmtpPort int

@description('Mail Sender Email')
param mailSenderEmailAddress string

@description('Mail Sender Password')
@secure()
param mailSenderEmailPassword string

@description('Mail Sender Display Name')
param mailSenderDisplayName string

@description('Mail dummy recipient address to overwrite')
param mailDummyRecipientAddress string

resource webIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

module webKeyVaultAccess '../core/security/keyvault-access.bicep' = {
  name: 'web-keyvault-access'
  scope: resourceGroup(keyVaultResourceGroupName)
  params: {
    principalId: webIdentity.properties.principalId
    keyVaultName: keyVault.name
  }
}

module app '../core/host/container-app-upsert.bicep' = {
  name: '${serviceName}-container-app'
  dependsOn: [webKeyVaultAccess]
  params: {
    name: name
    location: location
    tags: union(tags, { 'azd-service-name': serviceName })
    identityName: webIdentity.name
    imageName: imageName
    exists: exists
    serviceBinds: serviceBinds
    containerAppsEnvironmentName: containerAppsEnvironmentName
    containerRegistryName: containerRegistryName
    env: [
      {
        name: 'AZURE_CLIENT_ID'
        value: webIdentity.properties.clientId
      }
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
        value: !empty(applicationInsightsName) ? applicationInsights.properties.ConnectionString : ''
      }
      {
        name: 'AZURE_KEY_VAULT_ENDPOINT'
        value: keyVault.properties.vaultUri
      }
      {
        name: 'AZURE_STORAGE_BLOB_ENDPOINT'
        value: storageBlobEndpoint
      }
      {
        name: 'AZURE_STORAGE_CONTAINER'
        value: storageContainerName
      }
      {
        name: 'AZURE_SEARCH_SERVICE_ENDPOINT'
        value: searchServiceEndpoint
      }
      {
        name: 'AZURE_SEARCH_INDEX'
        value: searchIndexName
      }
      {
        name: 'AZURE_FORMRECOGNIZER_SERVICE_ENDPOINT'
        value: formRecognizerEndpoint
      }
      {
        name: 'AZURE_OPENAI_ENDPOINT'
        value: openAiEndpoint
      }
      {
        name: 'AZURE_OPENAI_CHATGPT_DEPLOYMENT'
        value: openAiChatGptDeployment
      }
      {
        name: 'AZURE_OPENAI_EMBEDDING_DEPLOYMENT'
        value: openAiEmbeddingDeployment
      }
      {
        name: 'AZURE_COMPUTER_VISION_ENDPOINT'
        value: computerVisionEndpoint
      }
      {
        name: 'USE_VISION'
        value: useVision ? 'true' : 'false'
      }
      {
        name: 'OPENAI_API_KEY'
        value: openAiApiKey
      }
      {
        name: 'AZURE_POSTGRESQL_CONNECTION_STRING'
        value: postgresConnectionString
      }
      {
        name: 'MAIL_SMTP_HOST'
        value: mailSmtpHost
      }
      {
        name: 'MAIL_SMTP_PORT'
        value: mailSmtpPort
      }
      {
        name: 'MAIL_SENDER_EMAIL_ADDRESS'
        value: mailSenderEmailAddress
      }
      {
        name: 'MAIL_SENDER_EMAIL_PASSWORD'
        value: mailSenderEmailPassword
      }
      {
        name: 'MAIL_SENDER_DISPLAY_NAME'
        value: mailSenderDisplayName
      }
      {
        name: 'MAIL_DUMMY_RECIPIENT_ADDRESS'
        value: mailDummyRecipientAddress
      }
    ]
    targetPort: 8080
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = if (!empty(applicationInsightsName)) {
  name: applicationInsightsName
}

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
  scope: resourceGroup(keyVaultResourceGroupName)
}

output SERVICE_WEB_IDENTITY_NAME string = identityName
output SERVICE_WEB_IDENTITY_PRINCIPAL_ID string = webIdentity.properties.principalId
output SERVICE_WEB_IMAGE_NAME string = app.outputs.imageName
output SERVICE_WEB_NAME string = app.outputs.name
output SERVICE_WEB_URI string = app.outputs.uri
