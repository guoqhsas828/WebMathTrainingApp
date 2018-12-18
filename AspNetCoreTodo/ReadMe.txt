git clone https://github.com/azure-samples/dotnetcore-sqldb-tutorial
cd dotnetcore-sqldb-tutorial
//Run the following commands to install the required packages, run database migrations and start the app 
dotnet restore
dotnet ef database update
dotnet run

//Open Azure Cloud Shell
//Create Resource group
az group create --name myResourceGroup --location "East US"
//Create SQL database server
az sql server create --name <server_name> --resource-group myResourceGroup --location "East US" --admin-user <db_username> --admin-password <db_password>
//Create firewall rule, which only allows azure resource access
az sql server firewall-rule create --resource-group myResourceGroup --server <server_name> --name AllowYourIp --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
//Create the sql server database
az sql db create --resource-group myResourceGroup --server <server_name> --name coreDB --service-objective S0
//Connection String
Server=tcp:<server_name>.database.windows.net,1433;Database=coreDB;User ID=<db_username>;Password=<db_password>;Encrypt=true;Connection Timeout=30;
//Create deployment user account
az webapp deployment user set --user-name <username> --password <password>
//Create an app service plan
az appservice plan create --name myAppServicePlan --resource-group myResourceGroup --sku FREE
//Create a web app in the app service plan
az webapp create --resource-group myResourceGroup --plan myAppServicePlan --name <app_name> --deployment-local-git
//Create an environment variable
az webapp config connection-string set --resource-group myResourceGroup --name <app name> --settings MyDbConnection='<connection_string>' --connection-string-type SQLServer
//Create an app settings
az webapp config appsettings set --name <app_name> --resource-group myResourceGroup --settings ASPNETCORE_ENVIRONMENT="Production"
Connect to SQL Database in production
In your local repository, open Startup.cs and find the following code:
C#

Copy
services.AddDbContext<MyDatabaseContext>(options =>
        options.UseSqlite("Data Source=localdatabase.db"));
Replace it with the following code, which uses the environment variables that you configured earlier.
C#

Copy
// Use SQL Database if in Azure, otherwise, use SQLite
if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
    services.AddDbContext<MyDatabaseContext>(options =>
            options.UseSqlServer(Configuration.GetConnectionString("MyDbConnection")));
else
    services.AddDbContext<MyDatabaseContext>(options =>
            options.UseSqlite("Data Source=localdatabase.db"));

// Automatically perform database migration
services.BuildServiceProvider().GetService<MyDatabaseContext>().Database.Migrate();
If this code detects that it is running in production (which indicates the Azure environment), then it uses the connection string you configured to connect to the SQL Database.
The Database.Migrate() call helps you when it is run in Azure, because it automatically creates the databases that your .NET Core app needs, based on its migration configuration. 
//Local git actions
git add .
git commit -m "connect to SQLDB in Azure"
git remote add azure <deploymentLocalGitUrl-from-create-step>
git push azure master

//Make further entity schema change,Run a few commands to make updates to your local database.
dotnet ef migrations add AddProperty
dotnet ef database update

//Publish changes to Azure
git add .
git commit -m "added done field"
git push azure master