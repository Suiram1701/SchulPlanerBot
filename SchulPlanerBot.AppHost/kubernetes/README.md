# Deployment using Kubernetes

## Deploy:

This describes how to deploy this service properly using kubernetes.
- Open a shell
- Adjust the version of SchulPlanerBot (discord-bot) in `SchulPlanerBot/SchulPlanerBot.csproj`.
- Navigate to the App host project (`cd SchulPlanerBot.AppHost`) if you're already there.
- Run `aspirate generate -ct "latest;vX.X.X.X" -o ./kubernetes/aspirate-output` (replace the X's with the real version) to generate the latest kubernetes configuration and publish the newest docker image.
- Run `aspirate apply -i ./kubernetes` to apply the changes to your cluster.

## Delete:

If you want to remove this deployment why ever you can just run
- Ensure the aspirate output exists.
- Navigate to the App host project (`cd SchulPlanerBot.AppHost`) if you're already there.
- Run `aspirate destroy -i ./kubernetes/aspirate-output`.

## Debug:

You might want to debug new features for the bot while using the same Discord application.
To achieve this you have to stop your production instance that your local client can respond to requests.
- Open a shell
- Stopping: `kubectl scale deployment/discord-bot -n schulplanerbot --replicas=0`
- Starting again: `kubectl scale deployment/discord-bot -n schulplanerbot --replicas=1`