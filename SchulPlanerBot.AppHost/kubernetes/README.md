# Deployment using Kubernetes

This describes how to deploy this service properly using kubernetes:
- Remove the `aspirate-output` if available
- Open the shell of your trust.
- Navigate to the App host project (`cd SchulPlanerBot.AppHost`).
- Run `aspirate generate --image-pull-policy IfNotPresent` to generate the latest kubernetes configuration.
- Run `aspirate apply` to apply the changes to your cluster.
- Run `kubectl apply -f ./kubernetes/external-services.yaml` to configure the node ports for every external accessible services on your cluster.