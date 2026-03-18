using Pulumi;
using Pulumi.AzureNative.ContainerRegistry;
using Pulumi.DockerBuild;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MedInsightAzureAppService
{
    /// <summary>
    /// Arguments for the DockerImage component.
    /// </summary>
    public class DockerImageArgs
    {
        public Input<string> ResourceGroupName { get; set; } = null!;
        public Input<string> Location { get; set; } = null!;
        public string AppName { get; set; } = null!;
        public string DockerContext { get; set; } = "./docker";
        public string RegistryName { get; set; } = null!;
    }

    /// <summary>
    /// A component resource that encapsulates an Azure Container Registry
    /// and builds/pushes a Docker image to it.
    /// </summary>
    public class DockerImage : ComponentResource
    {
        public Registry Registry { get; private set; }
        public Image Image { get; private set; }
        public Output<string> ImageName { get; private set; }
        public Output<string> RegistryServer { get; private set; }
        public Output<string> RegistryUsername { get; private set; }
        public Output<string> RegistryPassword { get; private set; }

        public DockerImage(string name, DockerImageArgs args, ComponentResourceOptions? opts = null)
            : base("custom:azure:DockerImage", name, opts)
        {
            // Sanitize registry name: ACR only allows alphanumeric characters (5-50 chars)
            var sanitizedRegistryName = SanitizeRegistryName(args.RegistryName);

            // Azure Container Registry
            Registry = new Registry($"{name}-registry", new Pulumi.AzureNative.ContainerRegistry.RegistryArgs
            {
                ResourceGroupName = args.ResourceGroupName,
                Location = args.Location,
                RegistryName = sanitizedRegistryName,
                Sku = new Pulumi.AzureNative.ContainerRegistry.Inputs.SkuArgs
                {
                    Name = "Basic",
                },
                AdminUserEnabled = true,
            }, new CustomResourceOptions { Parent = this });

            // Get registry credentials
            var credentials = ListRegistryCredentials.Invoke(new ListRegistryCredentialsInvokeArgs
            {
                ResourceGroupName = args.ResourceGroupName,
                RegistryName = Registry.Name,
            });

            var adminUsername = credentials.Apply(c => c.Username!);
            var adminPassword = credentials.Apply(c => c.Passwords[0].Value!);

            // Build image name
            ImageName = Output.Format($"{Registry.LoginServer}/{args.AppName}:latest");

            // Build and push Docker image
            Image = new Image($"{name}-image", new ImageArgs
            {
                Tags = new InputList<string> { ImageName },
                Context = new Pulumi.DockerBuild.Inputs.BuildContextArgs
                {
                    Location = args.DockerContext,
                },
                Platforms = new InputList<Platform>
                {
                    Platform.Linux_amd64,
                },
                Push = true,
                Registries = new InputList<Pulumi.DockerBuild.Inputs.RegistryArgs>
                {
                    new Pulumi.DockerBuild.Inputs.RegistryArgs
                    {
                        Address = Registry.LoginServer,
                        Username = adminUsername,
                        Password = adminPassword,
                    },
                },
            }, new CustomResourceOptions { Parent = this });

            // Store outputs for easy access
            RegistryServer = Registry.LoginServer;
            RegistryUsername = adminUsername;
            RegistryPassword = adminPassword;

            // Register outputs
            var outputs = new Dictionary<string, object?>
            {
                ["registryName"] = Registry.Name,
                ["registryLoginServer"] = Registry.LoginServer,
                ["imageName"] = ImageName,
                ["imageRef"] = Image.Ref,
            };

            RegisterOutputs(outputs);
        }

        /// <summary>
        /// Sanitizes a registry name to comply with Azure Container Registry naming rules.
        /// ACR names must be alphanumeric only, 5-50 characters long.
        /// </summary>
        private static string SanitizeRegistryName(string registryName)
        {
            // Remove all non-alphanumeric characters and convert to lowercase
            var sanitized = Regex.Replace(registryName, "[^a-zA-Z0-9]", "").ToLower();

            // Ensure minimum length of 5 characters
            if (sanitized.Length < 5)
            {
                sanitized += "registry";
            }

            // Ensure maximum length of 50 characters
            if (sanitized.Length > 50)
            {
                sanitized = sanitized.Substring(0, 50);
            }

            return sanitized;
        }
    }
}
