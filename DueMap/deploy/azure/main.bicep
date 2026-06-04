// =============================================================================
// DueMap — Azure deploy
// Target: Azure Container Apps + Azure SQL serverless
// Scope:  resourceGroup
//
// Cost shape (per the project README's cost analysis):
//   ~$70/mo idle in dev, ~$200–300/mo at 10 paying PMs.
//
// Resources:
//   - Log Analytics workspace (mandatory dependency of Container Apps env)
//   - Container Apps managed environment
//   - Azure Container Registry (Standard SKU; Basic is fine but doesn't have geo-replication)
//   - Azure SQL logical server + serverless DB (auto-pause after 1h)
//   - Storage account + Azure Files share for ASP.NET Core Data Protection keys
//   - User-assigned managed identity (ACR pull + Storage access)
//   - Container App: Web (ingress, scales 0–3)
//   - Container App: Worker (internal only, fixed 1 replica — Hangfire likes a single executor)
// =============================================================================

targetScope = 'resourceGroup'

@description('Azure region. Container Apps + SQL Serverless availability varies; eastus / westus3 / westeurope are safe bets.')
param location string = resourceGroup().location

@description('Short environment tag used to disambiguate resources in one subscription. Examples: dev, staging, prod.')
@minLength(2)
@maxLength(8)
param environmentName string = 'dev'

@description('Stable name prefix. Combined with environmentName to form globally-unique resource names.')
@minLength(3)
@maxLength(11)
param namePrefix string = 'duemap'

@description('SQL admin login. Cannot be "admin", "sa", or other reserved names.')
@minLength(4)
param sqlAdminLogin string

@description('SQL admin password. Surface this via Azure Key Vault reference for prod, not a raw value.')
@secure()
param sqlAdminPassword string

@description('Container image tag for the Web app. Push to the ACR created here, then redeploy.')
param webImageTag string = 'web:initial'

@description('Container image tag for the Worker. Push to the ACR created here, then redeploy.')
param workerImageTag string = 'worker:initial'

@description('SendGrid API key — empty in dev to disable real sends.')
@secure()
param sendGridApiKey string = ''

@description('Twilio Auth Token — empty in dev to disable real sends.')
@secure()
param twilioAuthToken string = ''

@description('QuickBooks OAuth client secret — empty until you register the app at Intuit.')
@secure()
param quickBooksClientSecret string = ''

@description('Xero OAuth client secret — empty until you register the app at Xero.')
@secure()
param xeroClientSecret string = ''

// -----------------------------------------------------------------------------
// Derived names. All globally-unique resources include a short hash so deploys
// to the same subscription don't collide.
// -----------------------------------------------------------------------------
var uniqueSuffix = toLower(uniqueString(resourceGroup().id, environmentName))
var baseName     = '${namePrefix}-${environmentName}'
var acrName      = toLower('${namePrefix}${environmentName}${uniqueSuffix}')
var storageName  = toLower('${namePrefix}${environmentName}st${take(uniqueSuffix, 6)}')
var sqlServerName = toLower('${baseName}-sql-${take(uniqueSuffix, 6)}')

// =============================================================================
// LOG ANALYTICS
// =============================================================================
resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${baseName}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// =============================================================================
// CONTAINER REGISTRY
// =============================================================================
resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: acrName
  location: location
  sku: { name: 'Standard' }
  properties: {
    adminUserEnabled: false   // managed identity does the pull
  }
}

// =============================================================================
// MANAGED IDENTITY (used by both container apps for ACR pull + storage access)
// =============================================================================
resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${baseName}-id'
  location: location
}

// ACR pull role on the registry for our identity.
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: acr
  name: guid(acr.id, appIdentity.id, 'AcrPull')
  properties: {
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    // AcrPull built-in role
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  }
}

// =============================================================================
// SQL — server + serverless database with auto-pause
// =============================================================================
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    publicNetworkAccess: 'Enabled'
    minimalTlsVersion: '1.2'
  }
}

// Allow Azure services (Container Apps) to reach this SQL server.
resource allowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'   // Special-case for "Azure services"
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'DueMap'
  location: location
  sku: {
    name: 'GP_S_Gen5_2'   // General Purpose, Serverless, 2 vCore max
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    autoPauseDelay: 60       // Pause after 60 min of inactivity
    minCapacity: json('0.5') // Float-encoded as JSON per ARM convention
    maxSizeBytes: 34359738368  // 32 GB
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
  }
}

// =============================================================================
// STORAGE — Azure Files share for Data Protection keys (shared by Web + Worker)
// =============================================================================
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

resource storageFile 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource dpKeyShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: storageFile
  name: 'dp-keys'
  properties: {
    shareQuota: 5   // GB — keys are tiny
  }
}

// =============================================================================
// CONTAINER APPS ENVIRONMENT
// =============================================================================
resource caEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${baseName}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
    zoneRedundant: false
  }
}

// Mount the Azure Files share into the env so apps can volume-mount it.
resource caEnvStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: caEnv
  name: 'dp-keys'
  properties: {
    azureFile: {
      accountName: storage.name
      accountKey: storage.listKeys().keys[0].value
      shareName: dpKeyShare.name
      accessMode: 'ReadWrite'
    }
  }
}

// =============================================================================
// COMMON ENV / SECRET DEFINITIONS for both container apps
// =============================================================================
var sqlConnString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDb.name};User Id=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=true;TrustServerCertificate=False;Connection Timeout=30'

var sharedSecrets = [
  { name: 'sql-connection-string', value: sqlConnString }
  { name: 'sendgrid-api-key',      value: sendGridApiKey }
  { name: 'twilio-auth-token',     value: twilioAuthToken }
  { name: 'quickbooks-client-secret', value: quickBooksClientSecret }
  { name: 'xero-client-secret',    value: xeroClientSecret }
]

var sharedEnv = [
  { name: 'ConnectionStrings__Default',     secretRef: 'sql-connection-string' }
  { name: 'Integrations__SendGrid__ApiKey', secretRef: 'sendgrid-api-key' }
  { name: 'Integrations__Twilio__AuthToken', secretRef: 'twilio-auth-token' }
  { name: 'Integrations__QuickBooks__ClientSecret', secretRef: 'quickbooks-client-secret' }
  { name: 'Integrations__Xero__ClientSecret', secretRef: 'xero-client-secret' }
  { name: 'DataProtection__KeyRingPath',    value: '/var/duemap/keys' }
]

// =============================================================================
// CONTAINER APP — WEB (ingress)
// =============================================================================
resource webApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${baseName}-web'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${appIdentity.id}': {} }
  }
  properties: {
    managedEnvironmentId: caEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [{
        server: acr.properties.loginServer
        identity: appIdentity.id
      }]
      secrets: sharedSecrets
    }
    template: {
      containers: [{
        name: 'web'
        image: '${acr.properties.loginServer}/${webImageTag}'
        resources: {
          cpu: json('0.5')
          memory: '1Gi'
        }
        env: concat(sharedEnv, [
          { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
          { name: 'ASPNETCORE_URLS',        value: 'http://+:8080' }
        ])
        volumeMounts: [{
          volumeName: 'dp-keys'
          mountPath: '/var/duemap/keys'
        }]
      }]
      scale: {
        minReplicas: 0
        maxReplicas: 3
        rules: [{
          name: 'http-concurrency'
          http: { metadata: { concurrentRequests: '50' } }
        }]
      }
      volumes: [{
        name: 'dp-keys'
        storageType: 'AzureFile'
        storageName: caEnvStorage.name
      }]
    }
  }
  dependsOn: [ acrPullRole ]
}

// =============================================================================
// CONTAINER APP — WORKER (internal only; Hangfire executes here)
// =============================================================================
resource workerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${baseName}-worker'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${appIdentity.id}': {} }
  }
  properties: {
    managedEnvironmentId: caEnv.id
    configuration: {
      // No ingress block — worker is purely internal.
      registries: [{
        server: acr.properties.loginServer
        identity: appIdentity.id
      }]
      secrets: sharedSecrets
    }
    template: {
      containers: [{
        name: 'worker'
        image: '${acr.properties.loginServer}/${workerImageTag}'
        resources: {
          cpu: json('0.5')
          memory: '1Gi'
        }
        env: concat(sharedEnv, [
          { name: 'DOTNET_ENVIRONMENT', value: 'Production' }
        ])
        volumeMounts: [{
          volumeName: 'dp-keys'
          mountPath: '/var/duemap/keys'
        }]
      }]
      scale: {
        // Exactly one Hangfire executor. Scaling out duplicates job execution.
        minReplicas: 1
        maxReplicas: 1
      }
      volumes: [{
        name: 'dp-keys'
        storageType: 'AzureFile'
        storageName: caEnvStorage.name
      }]
    }
  }
  dependsOn: [ acrPullRole ]
}

// =============================================================================
// OUTPUTS
// =============================================================================
output webFqdn string = webApp.properties.configuration.ingress.fqdn
output acrLoginServer string = acr.properties.loginServer
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDb.name
output managedIdentityClientId string = appIdentity.properties.clientId
