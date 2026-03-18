using Pulumi;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
using System.Collections.Generic;

namespace MedInsightAzureAppService
{
    /// <summary>
    /// Arguments for the ContainerApp component.
    /// </summary>
    public class ContainerAppArgs
    {
        public Input<string> ResourceGroupName { get; set; } = null!;
        public Input<string> Location { get; set; } = null!;
        public string AppName { get; set; } = null!;
        public string Sku { get; set; } = "S1";
        public Input<string> Image { get; set; } = null!;
        public Input<string>? RegistryServer { get; set; }
        public Input<string>? RegistryUsername { get; set; }
        public Input<string>? RegistryPassword { get; set; }
    }

    /// <summary>
    /// A component resource that encapsulates an Azure App Service Plan
    /// and Web App configured to run a container, with optional deployment slots.
    /// </summary>
    public class ContainerApp : ComponentResource
    {
        public AppServicePlan AppServicePlan { get; private set; }
        public WebApp App { get; private set; }
        public Dictionary<string, WebAppSlot> Slots { get; private set; } = new Dictionary<string, WebAppSlot>();

        public ContainerApp(string name, ContainerAppArgs args, ComponentResourceOptions? opts = null)
            : base("custom:azure:ContainerApp", name, opts)
        {
            // App Service Plan
            AppServicePlan = new AppServicePlan($"{name}-plan", new AppServicePlanArgs
            {
                ResourceGroupName = args.ResourceGroupName,
                Location = args.Location,
                Name = $"{args.AppName}-plan",
                Kind = "Linux",
                Reserved = true, // required for Linux
                Sku = new SkuDescriptionArgs
                {
                    Name = args.Sku,
                    Tier = "Standard",
                },
            }, new CustomResourceOptions { Parent = this });


            var appSettings = new InputList<NameValuePairArgs>
                {
                    new NameValuePairArgs
                    {
                        Name = "WEBSITES_ENABLE_APP_SERVICE_STORAGE",
                        Value = "false",
                    },
                    new NameValuePairArgs
                    {
                        Name = "DOCKER_REGISTRY_SERVER_URL",
                        Value = args.RegistryServer != null
                            ? args.RegistryServer.Apply(s => $"https://{s}")
                            : Output.Create("https://index.docker.io"),
                    },
                    new NameValuePairArgs
                    {
                        Name = "DOCKER_REGISTRY_SERVER_USERNAME",
                        Value = args.RegistryUsername ?? Output.Create(""),
                    },
                    new NameValuePairArgs
                    {
                        Name = "DOCKER_REGISTRY_SERVER_PASSWORD",
                        Value = args.RegistryPassword ?? Output.Create(""),
                    }
                };

            // App Service (Web App) - Production
            App = new WebApp($"{name}-app", new WebAppArgs
            {
                ResourceGroupName = args.ResourceGroupName,
                Location = args.Location,
                Name = $"{args.AppName}-app",
                ServerFarmId = AppServicePlan.Id,
                Kind = "app,linux,container",
                SiteConfig = new SiteConfigArgs
                {
                    LinuxFxVersion = args.Image.Apply(img => $"DOCKER|{img}"),
                    AlwaysOn = false, // must be False on Basic tier
                    AppSettings = appSettings.Apply(settings =>
                    {
                        var list = new List<NameValuePairArgs>(settings)
                        {
                            new NameValuePairArgs
                            {
                                Name = "ENVIRONMENT",
                                Value = "production",
                            },
                        };
                        return list;
                    }),
                },
                HttpsOnly = true,
            }, new CustomResourceOptions { Parent = this });

            var stagingSlot = new WebAppSlot($"{name}-staging-slot", new WebAppSlotArgs
            {
                ResourceGroupName = args.ResourceGroupName,
                Name = App.Name,
                Slot = "staging",
                ServerFarmId = AppServicePlan.Id,
                Kind = "app,linux,container",
                SiteConfig = new SiteConfigArgs
                {
                    LinuxFxVersion = args.Image.Apply(img => $"DOCKER|{img}"),
                    AlwaysOn = false,
                    AppSettings = appSettings.Apply(settings =>
                    {
                        var list = new List<NameValuePairArgs>(settings)
                        {
                            new NameValuePairArgs
                            {
                                Name = "ENVIRONMENT",
                                Value = "staging",
                            },
                        };
                        return list;
                    }),
                },
                HttpsOnly = true,
            }, new CustomResourceOptions { Parent = App });

            // Register outputs
            var outputs = new Dictionary<string, object?>
            {
                ["appServicePlanName"] = AppServicePlan.Name,
                ["appName"] = App.Name,
                ["appUrl"] = App.DefaultHostName.Apply(h => $"https://{h}"),
                ["stagingSlotName"] = stagingSlot.Name,
                ["stagingSlotUrl"] = stagingSlot.DefaultHostName.Apply(h => $"https://{h}"),
            };

            RegisterOutputs(outputs);
        }
    }
}
