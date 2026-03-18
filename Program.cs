using Pulumi;
using Pulumi.AzureNative.Resources;
using System.Threading.Tasks;
using System.Collections.Generic;
using Config = Pulumi.Config;

namespace MedInsightAzureAppService
{
    class Program
    {
        static Task<int> Main() => Pulumi.Deployment.RunAsync(() =>
        {
            var config = new Config();
            var azureConfig = new Config("azure-native");
            var location = azureConfig.Require("location");
            var appName = config.Get("appName") ?? "medinsight";

            // Resource Group
            var resourceGroup = new ResourceGroup("rg", new ResourceGroupArgs
            {
                ResourceGroupName = $"{appName}-rg",
                Location = location,
            });

            // Docker Image (encapsulates ACR and image build/push)
            var dockerImage = new DockerImage("docker-image", new DockerImageArgs
            {
                ResourceGroupName = resourceGroup.Name,
                Location = resourceGroup.Location,
                AppName = appName!,
                RegistryName = $"{appName}acr",
                DockerContext = "./docker",
            });

            // Container App (encapsulates App Service Plan and Web App)
            // Use Image.Ref (digest-based) instead of ImageName (tag-based) to ensure updates are detected
            var containerApp = new ContainerApp("container-app", new ContainerAppArgs
            {
                ResourceGroupName = resourceGroup.Name,
                Location = resourceGroup.Location,
                AppName = appName!,
                Image = dockerImage.Image.Ref,
                RegistryServer = dockerImage.RegistryServer,
                RegistryUsername = dockerImage.RegistryUsername,
                RegistryPassword = dockerImage.RegistryPassword,
            });

            // Exports
            return new Dictionary<string, object?>
            {
                ["resourceGroupName"] = resourceGroup.Name,
                ["registryName"] = dockerImage.Registry.Name,
                ["registryLoginServer"] = dockerImage.RegistryServer,
                ["imageName"] = dockerImage.ImageName,
                ["imageRef"] = dockerImage.Image.Ref,
                ["appServicePlanName"] = containerApp.AppServicePlan.Name,
                ["appName"] = containerApp.App.Name,
                ["appUrl"] = containerApp.App.DefaultHostName.Apply(h => $"https://{h}"),
            };
        });
    }
}
