# Dapr Workshop - C#

![Dapr sidecar](imgs/dapr_sidecar_pixelart.png)

This repository contains the starting point for you to complete the challenges from Diagrid's Dapr Workshop. This workshop is designed to serve as a quick starting point for you to become familiar with Dapr's most popular APIs and how to apply them to your microservices solutions.

When you are ready, navigate to [Diagrid's Dapr Workshop](https://github.com/diagrid-labs/dapr-workshop) and have fun!

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) (initialized via `dapr init`)
- A REST client such as [cURL](https://curl.se/) or the VSCode [REST Client extension](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)

Alternatively, open the repo in the included [devcontainer](./.devcontainer/) (VS Code + Docker Desktop, or a GitHub Codespace) to get a preconfigured environment.

## Running the workshop

The challenge instructions live in [Diagrid's Dapr Workshop](https://github.com/diagrid-labs/dapr-workshop). Clone this repo, follow the workshop instructions, and complete the challenges against the code in `start-here/` and `workflow-workshop/`.

## Project structure

- `start-here/` — Starting-point solution for the core Dapr APIs challenges. Contains the `DaprWorkshop.sln` with the `PizzaStorefront`, `PizzaOrder`, `PizzaKitchen`, `PizzaDelivery`, and `PizzaWorkflow` projects, plus an `Endpoints.http` file and shared `resources/`.
- `workflow-workshop/` — Starting-point solution for the Dapr Workflow challenges. Includes a `dapr.yaml` multi-app run file.
- `.devcontainer/` — Devcontainer definition for local or Codespaces use.
- `imgs/` — Images referenced by this README and the workshop instructions.

---

Join the [Dapr Discord](https://diagrid.ws/dapr-discord) for Q&A and chat with other community members!
