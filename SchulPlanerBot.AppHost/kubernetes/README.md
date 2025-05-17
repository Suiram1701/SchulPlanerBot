# Deployment using Kubernetes

## Deploy a new image:

If you only made changes in the SchulPlanerBot it is enough to update the image.
- Adjust the version of SchulPlanerBot (discord-bot) in `SchulPlanerBot/SchulPlanerBot.csproj`.
- Open a shell
- Navigate to the App host project (`cd SchulPlanerBot.AppHost`) if you're not already there.
- Run `aspirate build --runtime-identifier linux-arm64 -ct "latest;vX.X.X.X"` (replace the X's with the real version) to publish the image.
- Wait a few seconds and run `kubectl rollout restart deployments/discord-bot -n schulplanerbot` to let kubernetes pull the latest image.

## Deploy infrastructure changes:

This describes how to deploy this service properly when changes on the infrastructure were made.
- Adjust the version of SchulPlanerBot (discord-bot) in `SchulPlanerBot/SchulPlanerBot.csproj`.
- Open a shell
- Navigate to the App host project (`cd SchulPlanerBot.AppHost`) if you're not already there.
- By the dashboard will be patched to support HTTPS which requires a certificate in the secret `tls-secret`. 
  To disable this go into `patches/dashboard.yaml` and out comment everything below line 15.
- Run `aspirate generate --skip-build -o ./kubernetes/aspirate-output` to generate the latest kubernetes configuration. Adjust the arguments to customize your setup.
- Run `aspirate apply -i ./kubernetes` to apply the changes to your cluster.

## Debugging:

You might want to debug new features for the bot while using the same Discord application.
To achieve this you have to stop your production instance that your local client can respond to requests.
- Open a shell
- Stopping: `kubectl scale deployment/discord-bot -n schulplanerbot --replicas=0`
- Starting again: `kubectl scale deployment/discord-bot -n schulplanerbot --replicas=1`

## Delete the cluster:

If you want to remove this deployment why ever you can just run
- Ensure the aspirate output exists.
- Open a shell
- Navigate to the App host project (`cd SchulPlanerBot.AppHost`) if you're not already there.
- Run `aspirate destroy -i ./kubernetes/aspirate-output`.
