targetScope = 'resourceGroup'

@description('Base name used for all resources (e.g. "docvault"). 3-20 lowercase alphanumeric chars.')
@minLength(3)
@maxLength(20)
param appName string = 'docvault'

@description('Azure region. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Container image to deploy, e.g. ghcr.io/kavehrostami90/docvault:latest')
param containerImage string

@description('PostgreSQL connection string (from Neon or other provider).')
@secure()
param databaseConnectionString string



// ── Existing Container Apps Environment (pre-provisioned by env.bicep) ──────

resource env 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: '${appName}-env'
}

// ── Container App ───────────────────────────────────────────────────────────

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
      }
      secrets: [
        { name: 'db-connection-string', value: databaseConnectionString }
      ]
    }
    template: {
      containers: [
        {
          name: appName
          image: containerImage
          resources: {
            // Consumption plan free grant covers ~180K vCPU-s/month
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT',      value: 'Production' }
            { name: 'ConnectionStrings__Database',  secretRef: 'db-connection-string' }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health/alive', port: 8080, scheme: 'HTTP' }
              initialDelaySeconds: 5
              periodSeconds: 15
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health/ready', port: 8080, scheme: 'HTTP' }
              initialDelaySeconds: 5
              periodSeconds: 10
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0   // scale-to-zero keeps costs at zero when idle
        maxReplicas: 3
        rules: [
          {
            name: 'http-scaling'
            http: { metadata: { concurrentRequests: '20' } }
          }
        ]
      }
    }
  }
}

// ── Outputs ─────────────────────────────────────────────────────────────────

output appUrl string = 'https://${app.properties.configuration.ingress.fqdn}'
