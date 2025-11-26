# Deployment using Kubernetes

## Deploy a new image:

If you only made changes in the SchulPlanerBot it is enough to update the image.
- Adjust the version of SchulPlanerBot (schulplanerbot) in `SchulPlanerBot/SchulPlanerBot.csproj` and `SchulPlanerBot.AppHost/kubernetes/patches/patch-deployment.yaml`.
- Open a shell
- Navigate to the App host project (`cd SchulPlanerBot.AppHost`) if you're not already there.
- Run `aspirate build --runtime-identifier linux-arm64 -ct "vX.X.X.X;latest"` (replace the X's with the real version) to publish the image.
- Run `aspirate apply -i ./kubernetes` to apply the changes to your cluster.

## Deploy infrastructure changes:

This describes how to deploy this service properly when changes on the infrastructure were made.
- Adjust the version of SchulPlanerBot (discord-bot) in `SchulPlanerBot/SchulPlanerBot.csproj` and `SchulPlanerBot.AppHost/kubernetes/patches/patch-deployment.yaml`.
- Open a shell
- Navigate to the App host project (`cd SchulPlanerBot.AppHost`) if you're not already there.
- Run `aspirate generate --skip-build -o ./kubernetes/aspirate-output` to generate the latest kubernetes configuration. Adjust the arguments to customize your setup.
- Run `aspirate apply -i ./kubernetes` to apply the changes to your cluster.

## Debugging:

You might want to debug new features for the bot while using the same Discord application.
To achieve this you can send a `PUT /api/ignoredGuilds` request with a JSON array of test guild as payload to the production instance.
To reverse this you can send `DELETE /api/ignoredGuilds/{test guild}`.

## Delete the cluster:

If you want to remove this deployment why ever you can just run
- Ensure the aspirate output exists.
- Open a shell
- Navigate to the App host project (`cd SchulPlanerBot.AppHost`) if you're not already there.
- Run `aspirate destroy -i ./kubernetes/aspirate-output`.
