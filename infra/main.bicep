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

@description('Comma-separated allowed CORS origins. Must be set to the Static Web App URL in production.')
param corsAllowedOrigins string = ''

@description('JWT signing key — minimum 32 characters. Must be kept secret.')
@secure()
param jwtSigningKey string

@description('Email address for the seeded admin account.')
param adminEmail string = 'admin@docvault.local'

@description('Password for the seeded admin account.')
@secure()
param adminPassword string

// ── App Settings ───────────────────────────────────────────────────────────

var baseAppSettings = [
  { name: 'ASPNETCORE_ENVIRONMENT',      value: 'Production' }
  { name: 'ASPNETCORE_URLS',             value: 'http://+:8080' }
  { name: 'ConnectionStrings__Database', value: databaseConnectionString }
  { name: 'Cors__AllowedOrigins',        value: corsAllowedOrigins }
  { name: 'Auth__JwtSigningKey',         value: jwtSigningKey }
  { name: 'Auth__JwtIssuer',             value: 'docvault' }
  { name: 'Auth__JwtAudience',           value: 'docvault-ui' }
  { name: 'Auth__AccessTokenExpiryMinutes', value: '15' }
  { name: 'Auth__RefreshTokenExpiryDays',   value: '7' }
  { name: 'Auth__AdminEmail',            value: adminEmail }
  { name: 'Auth__AdminPassword',         value: adminPassword }
  { name: 'OpenAI__Model',               value: 'text-embedding-3-small' }
  { name: 'OpenAI__Dimensions',          value: '768' }
]

var allAppSettings = !empty(openAiApiKey)
  ? concat(baseAppSettings, [{ name: 'OpenAI__ApiKey', value: openAiApiKey }])
  : baseAppSettings

// ── App Service Plan ───────────────────────────────────────────────────────
// F1 (Free) has no "Always On" — upgrade to B1 for production workloads.

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appName}-plan'
  location: location
  kind: 'linux'
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  properties: {
    reserved: true
  }
}

// ── Web App ────────────────────────────────────────────────────────────────

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
      healthCheckPath: '/health/live'
      appSettings: allAppSettings
    }
  }
}

// ── Outputs ────────────────────────────────────────────────────────────────

output appUrl string = 'https://${app.properties.defaultHostName}'
output appName string = app.name
