# Cosmos Global Distribution Demos

## Introduction

This solution contains a series of benchmarks that demonstrate various concepts for distributed databases, particularly around consistency, latency and distance. The tests in this solution include:

### 1. Read latency between single region and multi-region replicated accounts

This test shows the difference in read latency for an account with a single master in SouthEast Asia region with a front end reading from it in West US 2. The next test shows the drastic improvement in latency with data locality when the account is replicated to West US 2.

### 2. Write latency for accounts with Eventual consistency vs. Strong consistency + impact of distance on Strong consistency

This test shows the difference in write latency for two accounts with replicas 1000 miles apart in West US 2 and Central US regions, one with Eventual consistency, the other with Strong consistency. There is a third test that shows the impact on latency when the distance between the regions is more than double the distance, demonstrating the speed of light impact on latency when using Strong consistency across large distances.

### 3. Write latency for Single-Master account versus Multi-Master account

This test shows the difference in write latency for a single-master account (master: East US 2, replica: West US 2) with a client in West US 2. The next test shows the impact on write latency when using a multi-master account (master: East US 2, West US 2) with a client in West US 2.

### 4. Multi-Master Conflict Resolution

This test shows the Last Write Wins and Merge Procedure conflict resolution modes as well as "Async" mode where conflicts are written to the Conflicts Feed.

### 5. Custom Synchronization

This test shows how to implement a custom synchronization between two regions. This allows you to have a lower level of consistency for a database with many replicas across great distances. This scenario shows an account with four regions (West US, West US 2, East US, East US 2) at Session level consistency but with Strong consistency between West US and West US 2. This provides for greater data durability (RPO = 0) without having to use Strong consistency across all regions and over very large distances. This demo includes a separate class that shows a simpler implementation of this you can more easily used without all the timer code.

## Provisioning Cosmos DB accounts

This solution requires nine different Cosmos DB accounts. Each of are configured differently to support the underlying test with different replication modes, consistency levels and regions.
To simplify this process, the `global-dist-demos.sh` bash script provisions all the accounts using Azure CLI. 

To prepare the Cosmos accounts for this solution, follow the steps below.

### Steps

- Open Azure Portal and login to your account
- Ensure the Directory + subscription (under your login top right) you want to create these accounts is selected
- Launch Bash in Azure Cloud Shell
- Upload `global-dist-demos.sh`
- Type `bash global-dist-demos.sh`
- Follow the prompts

This script can take about 40+ minutes to run and can go some time with no apparent activity. 

When complete it will output all of the Cosmos DB endpoints and keys you will need in this solution's app.config. You may want to copy these to a file or someplace secure after the script completes.

## Provision Windows VM as Host

These tests are designed to run from a Windows VM in West US 2. You will need to provision a Windows VM (Standard B4ms (4 vcpus, 16 GB memory)) with RDP enabled. After the VM has been provisioned, RDP into it and install Visual Studio 2017, then copy the solution folder to the VM, or connect VS to your forked repo and clone it locally to the VM. 

To run the demo, RDP into the VM, open the solution folder, launch the solution and press F5.

[!Note]
> You can run this demo from your local machine however the latency benchmarks will be dramatically slower and will not show actual, SLA-based Cosmos DB latency metrics.

## Initializing the Demos

After the accounts are provisioned you can launch the application. Before running any demos you must run the "Initialize" menu item first. Running Initialize will provision 9 databases and 11 containers. Throughput is set at the database level at 1000 RU/s. Only one database has multiple containers that shares this throughput.

[!IMPORTANT]
> This solution contains are 11 containers provisioned at 1000 RU/s each. It is recommended that you run "Initialize" each time you run this solution and then run "Clean up" when you are done. This will reduce your costs to the absolute minimum.
