targetScope = 'resourceGroup'

@description('Base name used for all resources (e.g. "docvault"). 3-20 lowercase alphanumeric chars.')
@minLength(3)
@maxLength(20)
param appName string = 'docvault'

@description('Azure region. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('PostgreSQL connection string (from Neon or other provider).')
@secure()
param databaseConnectionString string

@description('OpenAI API key. Leave empty to use FakeEmbeddingProvider.')
@secure()
param openAiApiKey string = ''

var useOpenAi = !empty(openAiApiKey)

var baseAppSettings = [
  { name: 'ASPNETCORE_ENVIRONMENT',  value: 'Production' }
  { name: 'OpenAI__Model',           value: 'text-embedding-3-small' }
  { name: 'OpenAI__Dimensions',      value: '1536' }
]
var openAiSetting = useOpenAi ? [{ name: 'OpenAI__ApiKey', value: openAiApiKey }] : []
var allAppSettings = concat(baseAppSettings, openAiSetting)

// ── App Service Plan (Free tier) ────────────────────────────────────────

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appName}-plan'
  location: location
  kind: 'linux'
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  properties: {
    reserved: true   // required for Linux
  }
}

// ── Web App ──────────────────────────────────────────────────────────────

resource app 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appCommandLine: 'dotnet DocVault.Api.dll'
      appSettings: allAppSettings
      connectionStrings: [
        {
          name: 'Database'
          connectionString: databaseConnectionString
          type: 'Custom'
        }
      ]
      // Note: healthCheckPath and alwaysOn are not available on Free tier
    }
  }
}

// ── Outputs ─────────────────────────────────────────────────────────────

output appUrl string = 'https://${app.properties.defaultHostName}'
