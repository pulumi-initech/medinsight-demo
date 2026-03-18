# Azure App Service with Custom Docker Image

A Pulumi template that deploys a custom Docker container to Azure App Service with Azure Container Registry using C# and component resources.

## Overview

This template demonstrates:

- **Provider**: `pulumi-azure-native`, `pulumi-docker-build`
- **Language**: C# (.NET 9.0)
- **Architecture**: Component resource pattern for reusable infrastructure
- **Resources**:
  - `azure-native:resources:ResourceGroup` — resource group for all resources
  - `azure-native:containerregistry:Registry` — Azure Container Registry for private images
  - `docker-build:Image` — builds and pushes Docker images with digest tracking
  - `azure-native:web:AppServicePlan` — Linux-based App Service Plan (Basic tier)
  - `azure-native:web:WebApp` — containerized web application
- **Components**:
  - `DockerImage` — encapsulates ACR and Docker image build/push
  - `ContainerApp` — encapsulates App Service Plan and Web App
- **Features**:
  - Custom Nginx-based container with your content
  - Automatic image rebuilds on Dockerfile changes
  - Digest-based image references for reliable updates
  - Private container registry with admin credentials
- **Outputs**:
  - `resourceGroupName` — name of the resource group
  - `registryName` — Azure Container Registry name
  - `registryLoginServer` — ACR login server URL
  - `imageName` — Docker image name with tag
  - `imageRef` — Docker image reference with digest
  - `appServicePlanName` — name of the App Service Plan
  - `appName` — name of the web app
  - `appUrl` — HTTPS URL of the deployed application

## Prerequisites

- An Azure subscription with sufficient permissions
- Azure CLI installed and authenticated (`az login`)
- .NET 9.0 SDK or later
- Pulumi CLI installed

## Project Structure

```plaintext
.
├── docker/
│   ├── Dockerfile                    # Custom Nginx container definition
│   ├── index.html                    # Custom HTML page
│   └── image.png                     # Your custom image
├── Program.cs                        # Main Pulumi program
├── DockerImage.cs                    # Component for ACR and image build
├── ContainerApp.cs                   # Component for App Service
├── MedInsightAzureAppService.csproj  # .NET project file
├── Pulumi.yaml                       # Project settings
├── PulumiTemplate.yaml               # Template metadata
├── Pulumi.dev.yaml                   # Stack configuration (dev environment)
└── README.md                         # This file
```

## Configuration

Configure the following settings:

```bash
# Required: Azure region
pulumi config set azure-native:location westus2

# Optional: Application name (defaults to "medinsight")
pulumi config set appName myapp
```

## Customizing Your Container

The template includes a custom Nginx container in the `docker/` directory:

1. **Edit `docker/index.html`** - Customize the HTML content
2. **Replace `docker/image.png`** - Add your own image
3. **Modify `docker/Dockerfile`** - Add additional files or configuration

The Docker image will automatically rebuild and deploy when you run `pulumi up`.

## Usage

### 1. Restore Dependencies

```bash
dotnet restore MedInsightAzureAppService.csproj
```

### 2. Preview Deployment

```bash
pulumi preview
```

### 3. Deploy the Stack

```bash
pulumi up
```

### 4. Access the Application

After deployment, get the application URL:

```bash
pulumi stack output appUrl
# Example output: https://medinsight-app.azurewebsites.net
```

### 5. Clean Up

To destroy all resources:

```bash
pulumi destroy
```

## Component Architecture

### `DockerImage` Component

Encapsulates Docker image building and Azure Container Registry:

- **Azure Container Registry** (Basic SKU)
  - Private image hosting
  - Admin credentials enabled
- **Docker Build** (using Pulumi DockerBuild)
  - Builds from `./docker` directory
  - Pushes to ACR with digest tracking
  - Platform: linux/amd64

**Usage:**

```csharp
var dockerImage = new DockerImage("docker-image", new DockerImageArgs
{
    ResourceGroupName = resourceGroup.Name,
    Location = resourceGroup.Location,
    AppName = "myapp",
    RegistryName = "myappacr",
    DockerContext = "./docker",
});
```

### `ContainerApp` Component

Encapsulates App Service Plan and Web App:

- **App Service Plan** (Linux, Basic B1 tier)
  - Reserved for Linux containers
  - Configurable SKU
- **Web App** (Container-based)
  - Pulls from private ACR
  - Digest-based image references (automatic updates)
  - HTTPS-only enforcement
  - ACR credentials configured

**Usage:**

```csharp
var containerApp = new ContainerApp("container-app", new ContainerAppArgs
{
    ResourceGroupName = resourceGroup.Name,
    Location = resourceGroup.Location,
    AppName = "myapp",
    Image = dockerImage.Image.Ref, // Digest-based reference
    RegistryServer = dockerImage.RegistryServer,
    RegistryUsername = dockerImage.RegistryUsername,
    RegistryPassword = dockerImage.RegistryPassword,
});
```

## How It Works

1. **Build Phase**: Pulumi DockerBuild builds your Docker image from `./docker/`
2. **Push Phase**: Image is pushed to Azure Container Registry with a unique digest
3. **Deploy Phase**: App Service pulls the image using digest reference
4. **Update Phase**: When Dockerfile changes, new digest triggers automatic redeployment

## Outputs

Access stack outputs:

```bash
# Get all outputs
pulumi stack output

# Get specific output
pulumi stack output appUrl
pulumi stack output resourceGroupName
```

## Using as a Template

To use this as a template for new projects:

```bash
# Create a new project from this template
pulumi new https://github.com/yourorg/azure-app-service-docker-template

# Or if published to Pulumi templates
pulumi new azure-app-service-docker-csharp
```

## Future Enhancements

Potential improvements:
- Add deployment slots for staging/production
- Integrate with Azure Key Vault for secrets
- Add Application Insights for monitoring
- Configure custom domains and SSL certificates
- Add auto-scaling rules
- Implement virtual network integration
- Add health check endpoints
- Implement CI/CD integration

## Troubleshooting

**403 Forbidden Error**: Ensure your Dockerfile properly sets file permissions:

```dockerfile
RUN chmod -R 755 /usr/share/nginx/html
```

**Image Not Updating**: This template uses digest-based references to ensure updates. Each `pulumi up` rebuilds the image with a new digest.

**ACR Authentication Issues**: Verify that `AdminUserEnabled = true` is set on the registry resource.

## Resources

- [Pulumi Documentation](https://www.pulumi.com/docs/)
- [Azure Native Provider](https://www.pulumi.com/registry/packages/azure-native/)
- [Docker Build Provider](https://www.pulumi.com/registry/packages/docker-build/)
- [Pulumi Templates Guide](https://www.pulumi.com/docs/pulumi-cloud/developer-portals/templates/)
- [Pulumi Community Slack](https://pulumi.com/community/)