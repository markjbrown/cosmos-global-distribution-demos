#!/bin/bash

echo "Create Cosmos accounts for Global Distribution Demos"
echo "----------------------------------------------------"
echo "This script can take up to 40 minutes to run and appears to not be doing anything at all while accounts are provisioned."
echo "When the script is complete it will output the entire set of Cosmos endpoints and keys to use in your app.config file"
echo ""
echo "To begin this script please enter the following prompts..."
echo ""
read -p "Enter a resource group name: " resourceGroupName
read -p "Enter a resource group region: " location
echo "To help ensure globally unique Cosmos account names, enter a random string"
read -p "Enter random string: " accountPrefix

# Create a resource group
az group create -n $resourceGroupName --location $location

echo "Single-Multi Region Demo"
#Single Region => Single-Master, Write Region: Southeast Asia, Consistency: Eventual
accountSuffix="-latency-single-region"
SingleRegionEndpoint=$accountPrefix$accountSuffix
az cosmosdb create -g $resourceGroupName -n $SingleRegionEndpoint --locations "South East Asia"=0 --default-consistency-level "Eventual"

#Multi-Region => Single-Master, Write Region: Southeast Asia, Read Region: West US 2, Consistency: Eventual
accountSuffix="-latency-multi-region"
MultiRegionEndpoint=$accountPrefix$accountSuffix
az cosmosdb create -g $resourceGroupName -n $MultiRegionEndpoint --locations "South East Asia"=0 "West US 2"=1 --default-consistency-level "Eventual"

echo "Consistency/Latency Demos"
#Eventual => Single-Master, Write Region: West US 2, Read Region: Central US, Consistency: Eventual
accountSuffix="-consistency-eventual"
EventualEndpoint=$accountPrefix$accountSuffix
az cosmosdb create -g $resourceGroupName -n $EventualEndpoint --locations "West US 2"=0 "Central US"=1 --default-consistency-level "Eventual"

#Strong 1K Miles => Single-Master, Write Region: West US 2, Read Region: Central US, Consistency: Strong
accountSuffix="-consistency-strong-1kmiles"
Strong1kMilesEndpoint=$accountPrefix$accountSuffix
az cosmosdb create -g $resourceGroupName -n $Strong1kMilesEndpoint --locations "West US 2"=0 "Central US"=1 --default-consistency-level "Strong"

#Strong 2K Miles Single-Master, Write Region: West US 2, Read Region: East US 2, Consistency: Strong
accountSuffix="-consistency-strong-2kmiles"
Strong2kMilesEndpoint=$accountPrefix$accountSuffix
az cosmosdb create -g $resourceGroupName -n $Strong2kMilesEndpoint --locations "West US 2"=0 "East US 2"=1 --default-consistency-level "Strong"

echo "Single-Multi Master and Conflict Demos"
#Single Master => Single-Master, Write Region: East US 2, Read Region: West US 2, Consistency: Eventual
accountSuffix="-single-master"
SingleMasterEndpoint=$accountPrefix$accountSuffix
az cosmosdb create -g $resourceGroupName -n $SingleMasterEndpoint --locations "East US 2"=0 "West US 2"=1 --default-consistency-level "Eventual"

#Multi-Master => Multi-Master, Write Region: East US 2, West US 2, North Europe, Consistency: Eventual
accountSuffix="-multi-master"
MultiMasterEndpoint=$accountPrefix$accountSuffix
az cosmosdb create -g $resourceGroupName -n $MultiMasterEndpoint --locations "East US 2"=0 "West US 2"=1 "North Europe"=2 --default-consistency-level "Eventual" --enable-multiple-write-locations true

echo "Custom Synchronization Demos"
#Strong Multi-Master, Write Region: West US 2, East US 2, West US, East US, Consistency: Strong
accountSuffix="-strong-multi-master"
StrongEndpoint=$accountPrefix$accountSuffix
az cosmosdb create -g $resourceGroupName -n $StrongEndpoint --locations "West US 2"=0 "East US 2"=1 "West US"=2 "West US"=3 --default-consistency-level "Strong" --enable-multiple-write-locations true

#Custom Multi-Master, Write Region: West US 2, East US 2, West US, East US, Consistency: Session
accountSuffix="-custom-multi-master"
CustomSyncEndpoint=$accountPrefix$accountSuffix
az cosmosdb create -g $resourceGroupName -n $CustomSyncEndpoint --locations "West US 2"=0 "East US 2"=1 "West US"=2 "West US"=3 --default-consistency-level "Session" --enable-multiple-write-locations true

echo "Copy this section below into a text file and use to enter your endpoints and keys for app.config"
echo "--------------------------------------------------------------------------------"
echo "SingleRegionEndpoint = "$(az cosmosdb show -g $resourceGroupName -n $SingleRegionEndpoint --query documentEndpoint --output tsv)
echo "SingleRegionKey = "$(az cosmosdb list-keys -g $resourceGroupName -n $SingleRegionEndpoint --query primaryMasterKey --output tsv)
echo "MultiRegionEndpoint = "$(az cosmosdb show -g $resourceGroupName -n $MultiRegionEndpoint --query documentEndpoint --output tsv)
echo "MultiRegionKey = "$(az cosmosdb list-keys -g $resourceGroupName -n $MultiRegionEndpoint --query primaryMasterKey --output tsv)
echo "EventualEndpoint = "$(az cosmosdb show -g $resourceGroupName -n $EventualEndpoint --query documentEndpoint --output tsv)
echo "EventualKey = "$(az cosmosdb list-keys -g $resourceGroupName -n $EventualEndpoint --query primaryMasterKey --output tsv)
echo "Strong1kMilesEndpoint = "$(az cosmosdb show -g $resourceGroupName -n $Strong1kMilesEndpoint --query documentEndpoint --output tsv)
echo "Strong1kMilesKey = "$(az cosmosdb list-keys -g $resourceGroupName -n $Strong1kMilesEndpoint --query primaryMasterKey --output tsv)
echo "Strong2kMilesEndpoint = "$(az cosmosdb show -g $resourceGroupName -n $Strong2kMilesEndpoint --query documentEndpoint --output tsv)
echo "Strong2kMilesKey = "$(az cosmosdb list-keys -g $resourceGroupName -n $Strong2kMilesEndpoint --query primaryMasterKey --output tsv)
echo "SingleMasterEndpoint = "$(az cosmosdb show -g $resourceGroupName -n $SingleMasterEndpoint --query documentEndpoint --output tsv)
echo "SingleMasterKey = "$(az cosmosdb list-keys -g $resourceGroupName -n $SingleMasterEndpoint --query primaryMasterKey --output tsv)
echo "MultiMasterEndpoint = "$(az cosmosdb show -g $resourceGroupName -n $MultiMasterEndpoint --query documentEndpoint --output tsv)
echo "MultiMasterKey = "$(az cosmosdb list-keys -g $resourceGroupName -n $MultiMasterEndpoint --query primaryMasterKey --output tsv)
echo "CustomSyncEndpoint = "$(az cosmosdb show -g $resourceGroupName -n $CustomSyncEndpoint --query documentEndpoint --output tsv)
echo "CustomSyncKey = "$(az cosmosdb list-keys -g $resourceGroupName -n $CustomSyncEndpoint --query primaryMasterKey --output tsv)
echo "StrongEndpoint = "$(az cosmosdb show -g $resourceGroupName -n $StrongEndpoint --query documentEndpoint --output tsv)
echo "StrongKey = "$(az cosmosdb list-keys -g $resourceGroupName -n $StrongEndpoint --query primaryMasterKey --output tsv)
echo "------------------------------------------------------------------------------------"
echo "Script Complete"