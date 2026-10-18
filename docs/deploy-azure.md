# Deploying GarageFlow to Azure (guide, not yet run)

This is a checklist for the day you have an Azure subscription. It was written from the Azure CLI documentation and has **not been executed**: if a flag is rejected, run the command with `--help` and adjust. Check the current pricing page before you create anything, and delete everything when you finish.

Target: **Azure Container Apps** runs the Docker image from this repository; **Azure SQL Database** (serverless, auto-pause) replaces the SQL Server container.

## What changes compared with localhost
| Localhost | Azure |
|---|---|
| `http://localhost:5080` | `https://<app>.<region>.azurecontainerapps.io` (HTTPS ends at Azure's proxy) |
| SQL Server container | Azure SQL Database (connection string uses `Encrypt=True`) |
| `.env` file | secrets and environment variables on the container app |
| demo users | the bootstrap Admin from `Bootstrap__*` |
| Swagger on | Swagger off (or `Swagger__Enabled=true` for a short demo) |

## Steps (PowerShell, after `az login`)
```powershell
$rg = "garageflow-rg"; $loc = "centralindia"
$suffix = Get-Random -Maximum 99999      # names below must be globally unique
$sql = "garageflow-sql-$suffix"; $acr = "garageflowacr$suffix"
az group create -n $rg -l $loc

# 1. Database (serverless: pauses when idle; look for the "free offer" flags in `az sql db create --help`)
az sql server create -g $rg -n $sql -l $loc -u gfadmin -p "<a long strong password>"
az sql server firewall-rule create -g $rg -s $sql -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
az sql db create -g $rg -s $sql -n GarageFlow --edition GeneralPurpose --compute-model Serverless --family Gen5 --capacity 1 --auto-pause-delay 60

# 2. Build the image in Azure (no local push needed)
az acr create -g $rg -n $acr --sku Basic --admin-enabled true
az acr build -r $acr -t garageflow-api:1 .

# 3. Run it
az containerapp env create -g $rg -n garageflow-env -l $loc
$acrPass = az acr credential show -n $acr --query "passwords[0].value" -o tsv
az containerapp create -g $rg -n garageflow-api --environment garageflow-env `
  --image "$acr.azurecr.io/garageflow-api:1" `
  --registry-server "$acr.azurecr.io" --registry-username $acr --registry-password $acrPass `
  --target-port 8080 --ingress external --min-replicas 1 --max-replicas 1 `
  --secrets "conn=Server=tcp:$sql.database.windows.net,1433;Database=GarageFlow;User ID=gfadmin;Password=<sql password>;Encrypt=True;TrustServerCertificate=False" "jwt=<40+ random characters>" "adminpw=<12+ characters>" `
  --env-vars ASPNETCORE_ENVIRONMENT=Production Database__MigrateOnStartup=true Hosting__BehindProxy=true `
             ConnectionStrings__GarageFlow=secretref:conn Jwt__SigningKey=secretref:jwt `
             Bootstrap__AdminEmail=you@example.com Bootstrap__AdminPassword=secretref:adminpw Bootstrap__ShopName="GarageFlow Hyderabad"
```
Why `--max-replicas 1`: the maintenance worker and the login rate limiter live inside each instance. Several instances would each run the jobs and keep separate counters. Moving the worker to a single dedicated job is the real fix (a known limit).

## Check it
```powershell
$fqdn = az containerapp show -g $rg -n garageflow-api --query properties.configuration.ingress.fqdn -o tsv
curl.exe -s "https://$fqdn/health"        # {"status":"Healthy"}
curl.exe -s "https://$fqdn/health/db"     # the database answers
az containerapp logs show -g $rg -n garageflow-api --tail 50   # look for the two "Bootstrap:" lines
```
Then log in with the bootstrap Admin, add technicians and bays, and remove `Bootstrap__AdminPassword`:
`az containerapp update -g $rg -n garageflow-api --remove-env-vars Bootstrap__AdminPassword`

## Clean up (stops all charges)
```powershell
az group delete -n $rg --yes --no-wait
```

## Without a card
Run `docker compose --profile api up -d` on your PC and share it temporarily with a tunnel such as Cloudflare's `cloudflared tunnel --url http://localhost:5080` (needs no account for a quick temporary link; check their current terms). Set `Hosting__BehindProxy=true` for that run so the rate limit sees the real visitor.
