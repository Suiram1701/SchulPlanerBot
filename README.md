# SchulPlanerBot

This is a simple Discord bot which is intended to be used on private guilds to manage homeworks and tests (so most likely for students). It uses [Discord.NET](https://github.com/discord-net/discord.net) to communicate with the Discord API and a PostgreSQL server for storing the data.
Additionally the Aspire Dashboard is used for simple telemetry visualisation.

## Deployment

This project uses Aspir8 and Kubernetes for publishing.

- Have a setup Kubernetes cluster available.
- Make sure the aspirate .NET tool is installed.
- Navigate to the AppHosts directory.
- Run `aspirate init` to initialize some default behaviors and values.
- Run `aspirate generate --skip-build -o ./kubernetes/aspirate-output` to generate the latest output and set your secrets (like the API token).
- Run `aspirate apply -i ./kubernetes` to apply everything to you Kubernetes cluster.

A few more commands for this project can you find [here](SchulPlanerBot.AppHost/kubernetes/).
