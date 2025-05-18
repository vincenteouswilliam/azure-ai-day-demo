metadata description = 'Creates an Azure PostgreSQL flexible server and database.'
param tags object = {}

@description('Location of the PostgreSQL server')
param location string

@description('Name of the PostgreSQL server')
param postgresServerName string

@description('Name of the PostgreSQL database')
param postgresDatabaseName string

@description('Administrator username for postgre server')
param administratorLogin string

@description('Administrator password for postgre server')
@secure()
param administratorLoginPassword string

@description('SKU Name for the PostgreSQL server')
param skuName string

@description('Tier for the PostgreSQL server')
param tier string

@description('Storage size in GB for the PostgreSQL server')
param storageSizeGB int

@description('PostgreSQL server version')
param version string

// PostgreSQL server definition
resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: postgresServerName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: tier
  }
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    version: version
    storage: {
      storageSizeGB: storageSizeGB
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
}

// PostgreSQL database definition
resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  name: postgresDatabaseName
  parent: postgresServer
}

// Firewall rule to allow access to PostgreSQL server
resource firewallRule 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgresServer
  name: 'AllowAZNTTAI'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '255.255.255.255'
  }
}

// Output definitions
output postgresServerName string = postgresServer.name
output postgresDatabaseName string = postgresDatabase.name
output postgresConnectionString string = 'Host=${postgresServerName}.postgres.database.azure.com;Port=5432;Database=${postgresDatabaseName};Username=${administratorLogin};Password=${administratorLoginPassword};SslMode=Require;'
output postgresLocalConnectionString string = 'postgresql://${administratorLogin}:${administratorLoginPassword}@${postgresServerName}.postgres.database.azure.com:5432/${postgresDatabaseName}?sslmode=require'
output postgresAdministratorLoginPassword string = administratorLoginPassword
