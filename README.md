# Cosmos Global Distribution Demos

## Introduction

This solution contains a series of benchmarks that demonstrate various concepts for distributed databases, particularly around consistency, latency and distance. The tests in this solution include:

### 1. Read latency between single region and multi-region replicated accounts

This test shows the difference in read latency for an account with a single master in SouthEast Asia region with a front end reading from it in West US 2. The next test shows the drastic improvement in latency with data locality when the account is replicated to West US 2.

### 2. Write latency for accounts with Eventual consistency vs. Strong consistency + impact of distance on Strong consistency

This test shows the difference in write latency for two accounts with replicas 1000 miles apart in West US 2 and Central US regions, one with Eventual consistency, the other with Strong consistency. There is a third test that shows the impact on latency when the distance between the regions is more than double the distance, demonstrating the speed of light impact on latency when using Strong consistency across large distances.

### 3. Read and write latency for Single-Master account versus Multi-Master account

This test shows the difference in read latency for a single-master account (master: East US 2, replica: West US 2) with a client in West US 2. The next test shows the impact on write latency when using a multi-master account (master: East US 2, West US 2) with a client in West US 2.

### 4. Multi-Master Conflict Resolution

This test shows the Last Write Wins and Merge Procedure conflict resolution modes as well as "Async" mode where conflicts are written to the Conflicts Feed.

### 5. Custom Synchronization

This test shows how to implement a custom synchronization between two regions. This allows you to have a lower level of consistency for a database with many replicas across great distances. This scenario shows an account with four regions (West US, West US 2, East US, East US 2) at Session level consistency but with Strong consistency between West US and West US 2. This provides for greater data durability (RPO = 0) without having to use Strong consistency across all regions and over very large distances. This demo includes a separate class that shows a simpler implementation of this you can more easily use without all the timer code.

## Provisioning Cosmos DB accounts

This solution requires nine different Cosmos DB accounts. Each of are configured differently to support the underlying test with different replication modes, consistency levels and regions.

To deploy the Cosmos DB accounts for this solution, follow the steps below.

### Steps

- Click "Deploy to Azure" below
- Enter a resource group name and location
- Enter a unique string to act as a prefix to make your Cosmos DB accounts unique
- When the script is complete, click on the Outputs tab
- Open the app.config for the solution on your local machine
- Copy the values for the endpoints and keys from each account to their corresponding placeholder in app.config

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fmarkjbrown%2Fcosmos-global-distribution-demos%2Fmaster%2FCosmosGlobalDistDemos%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

## Provision Windows VM as Host

These tests are designed to run from a Windows VM in West US 2. You will need a Windows VM with 2-4 CPU's and 8-16 GB of memory with RDP enabled. After the VM has been provisioned, RDP into it and install Visual Studio 2017, then copy the solution folder to the VM, or connect VS to your forked repo and clone it locally to the VM. Alternatively you can use the [Data Science VM](https://ms.portal.azure.com/#create/microsoft-dsvm.dsvm-windowsserver-2016) and just follow the remaining steps to copy the solution to that VM.

To run the demo, RDP into the VM, open the solution folder, launch the solution and press F5.

[!Note]
> You can run this demo from your local machine however the latency benchmarks will be dramatically slower and will not show actual, SLA-based Cosmos DB latency metrics.

## Initializing the Demos

After the accounts are provisioned you can launch the application. Before running any demos you must run the "Initialize" menu item first. Running Initialize will provision 9 databases and 11 containers. Throughput is set at the container level at 400 RU/s.

[!IMPORTANT]
> This solution has 11 containers provisioned at 1000 RU/s each which can be quite expensive if left provisioned. It is recommended that you run "Initialize" each time you run this solution and then run "Clean up" when you are done which will delete all the databases and containers for this sample. This will reduce your costs to the absolute minimum. Also be careful not to leave the VM running if not being used.
