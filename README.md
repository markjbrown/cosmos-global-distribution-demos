# Cosmos Global Distribution Demos

## Introduction

This solution contains a series of benchmarks that demonstrate various concepts for distributed databases, particularly around consistency, latency and distance. It also demonstrates the latency differences between single-master and multi-master databases, conflict resolution for multi-master databases and also how to implement custom synchronization in which you can have all regions at a lower level of consistency but have two regions strongly consistent, providing greater data durability but with less impact on latency.

## Provisioning Cosmos DB accounts

This solution requires nine different Cosmos DB accounts. Each of are configured differently to support the underlying test with different replication modes, consistency levels and regions.
To simplify this process, there is a Bash script which uses Azure CLI to provision all of the accounts called, "global-dist-demos.sh" in this solution. To prepare the Cosmos accounts for this solution, follow the steps below.

### Steps

- Open Azure Portal and login to your account
- Ensure the Directory + subscription (under your login top right) you want to create these accounts is selected
- Launch Bash in Azure Cloud Shell
- Upload 'global-dist-demos.sh'
- Type 'bash global-dist-demos.sh'
- Follow the prompts

This script can take about 40+ minutes to run and can go some time with no apparent activity. When it is complete it will output all of the Cosmos DB endpoints and keys you will need in this solution's app.config. It is recommended to copy these to a file or someplace secure after the script completes.

## Provision Windows VM as Host

These tests are designed to run from a Windows VM in West US 2. You will need to provision a Windows VM (Standard B4ms (4 vcpus, 16 GB memory)) with RDP enabled. After the VM has been provisioned, RDP into it and install Visual Studio 2017, then copy the solution folder to the VM. To run the demo, RDP into the VM, open the solution folder, launch the solution and press F5.

## Initializing the Demos

After the accounts are provisioned you can launch the application. Before running any demos you must run the "Initialize" menu item first. Running Initialize will provision 9 databases and 11 containers. Throughput is set at the database level at 1000 RU/s. Only one database has multiple containers that shares this throughput.

[!IMPORTANT]
> Even with database level throughput. There are 9 databases provisioned at 1000 RU/s each. This is about $17/day or $510/month. It is recommended that you run "Initialize" each time you run this solution and then run "Clean up" when you are done. This will reduce your costs to the absolute minimum.

