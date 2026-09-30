param location string = resourceGroup().location
param cosmosAccountName string
param databaseName string = 'trade-read-model'
param tradesContainerName string = 'trades'
param checkpointsContainerName string = 'synchronization-checkpoints'

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: cosmosAccountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
	databaseAccountOfferType: 'Standard'
	publicNetworkAccess: 'Disabled'
	disableLocalAuth: true
	locations: [
	  {
		locationName: location
		failoverPriority: 0
		isZoneRedundant: true
	  }

resource checkpointsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: checkpointsContainerName
  properties: {
	resource: {
	  id: checkpointsContainerName
	  partitionKey: {
		paths: [ '/partitionKey' ]
		kind: 'Hash'
	  }
	}
  }
}
	]
	consistencyPolicy: {
	  defaultConsistencyLevel: 'Session'
	}
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-11-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
	resource: {
	  id: databaseName
	}
  }
}

resource tradesContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: tradesContainerName
  properties: {
	resource: {
	  id: tradesContainerName
	  partitionKey: {
		paths: [ '/accountId' ]
		kind: 'Hash'
	  }
	  indexingPolicy: {
		indexingMode: 'consistent'
		automatic: true
		includedPaths: [
		  { path: '/*' }
		]
		compositeIndexes: [
		  [
			{ path: '/executedAt', order: 'descending' }
			{ path: '/id', order: 'ascending' }
		  ]
		]
	  }
	}
	options: {
	  autoscaleSettings: {
		maxThroughput: 4000
	  }
	}
  }
}

output cosmosEndpoint string = cosmosAccount.properties.documentEndpoint
