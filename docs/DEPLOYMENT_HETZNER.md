# Hetzner Cloud Deployment & Cloudflare Tunnel Setup Guide

This guide walks through deploying **social-worker** to a dedicated **Hetzner Cloud** VPS using **Cloudflare Tunnel (`cloudflared`)** for secure ingress and **GitHub Actions** for CI/CD automation.

---

## Architecture Overview

```
                      ┌───────────────────────────────────────────────┐
                      │              Cloudflare Edge                  │
                      │  - Terminate HTTPS for social.plopsaurus.xyz  │
                      │  - DDoS Protection & WAF                      │
                      └──────────────────────┬────────────────────────┘
                                             │ Outbound Tunnel
                                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Hetzner Cloud CX22 (Ubuntu 24.04)                                    │
│ Firewall: Only SSH (Port 22) allowed. Zero open web ports!          │
│                                                                     │
│  ┌─────────────────────── Docker Stack ──────────────────────────┐  │
│  │                                                               │  │
│  │  [cloudflared] ──(internal HTTP)──> [web:80]                  │  │
│  │                                        │                      │  │
│  │                                   [api:8080]                  │  │
│  │                                   /    │   \                  │  │
│  │                             [db] [transcriber] [image-engine] │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 1. Create Cloudflare Tunnel

1. Open the [Cloudflare Zero Trust Dashboard](https://one.dash.cloudflare.com/).
2. Navigate to **Networks** > **Tunnels**.
3. Click **Create a tunnel** (select **Cloudflared** as the connector type).
4. Give it a name (e.g. `social-worker-prod`).
5. In the installation command section, copy the **Tunnel Token** (the long string following `--token`).
6. Click **Next** to configure the public hostname:
   - **Subdomain**: `social`
   - **Domain**: `plopsaurus.xyz`
   - **Type**: `HTTP`
   - **URL**: `web:80` (or `localhost:8100`)
   - Under **Additional application settings** > **HTTP Settings**:
     - *Enable chunked encoding*: `On`
7. Save the tunnel.

---

## 2. Provision Server on Hetzner Cloud

1. Log into [Hetzner Cloud Console](https://console.hetzner.cloud/).
2. Click **Add Server**:
   - **Location**: Falkenstein (`fsn1`), Nuremberg (`nbg1`), or Helsinki (`hel1`).
   - **Image**: **Ubuntu 24.04 LTS**.
   - **Type**: Standard > **CX22** (2 vCPU, 4 GB RAM, 40 GB NVMe SSD) — ~€3.79/month.
   - **Public IPv4**: Yes.
   - **SSH Keys**: Add your personal public SSH key (`~/.ssh/id_ed25519.pub`).
   - **Name**: e.g., `social-worker-prod`.
3. Click **Create & Buy Now**. Note down your server's **Public IPv4**.

---

## 3. Initial Server Hardening & Docker Setup

SSH into your server as root:
```bash
ssh root@<YOUR_SERVER_IP>
```

### 3.1 Update System & Enable Firewall
Because Cloudflare Tunnel operates entirely over an outbound connection, **you do not need ports 80 or 443 open**:
```bash
apt update && apt upgrade -y
apt install -y curl ufw git fail2ban

# Configure UFW — ONLY SSH is needed!
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp    # SSH
ufw enable
```

### 3.2 Create Swapfile (Safety Buffer for Whisper Inference)
```bash
fallocate -l 2G /swapfile
chmod 600 /swapfile
mkswap /swapfile
swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab
```

### 3.3 Install Docker Engine & Compose Plugin
```bash
curl -fsSL https://get.docker.com -o get-docker.sh
sh get-docker.sh
systemctl enable --now docker
```

### 3.4 Create Deploy User & Directories
```bash
useradd -m -s /bin/bash deploy
usermod -aG docker deploy

mkdir -p /home/deploy/.ssh
chmod 700 /home/deploy/.ssh

# Generate deploy SSH keypair
ssh-keygen -t ed25519 -f /home/deploy/.ssh/id_ed25519 -N ""
cat /home/deploy/.ssh/id_ed25519.pub >> /home/deploy/.ssh/authorized_keys
chmod 600 /home/deploy/.ssh/authorized_keys
chown -R deploy:deploy /home/deploy/.ssh

# Prepare application directory
mkdir -p /opt/social-worker/searxng
chown -R deploy:deploy /opt/social-worker
```

Print the private key (save this for GitHub Secrets in Step 4):
```bash
cat /home/deploy/.ssh/id_ed25519
```

### 3.5 Create Production `.env`
Create `/opt/social-worker/.env`:
```bash
cat << 'EOF' > /opt/social-worker/.env
# Cloudflare Tunnel
CLOUDFLARE_TUNNEL_TOKEN=eyJh...YOUR_CLOUDFLARE_TUNNEL_TOKEN_HERE...

# Core Database
POSTGRES_PASSWORD=generate_a_secure_password_here

# LLM Configuration
LLM__BaseUrl=https://openrouter.ai/api/v1
LLM__ApiKey=your_openrouter_or_openai_key
LLM__Model=anthropic/claude-3.5-sonnet

# Authentication & Security
Auth__JwtSecret=generate_a_random_64_char_secret_key_here
Auth__AdminPassword=your_secure_admin_password
Auth__AccessTokenLifetimeMinutes=60
Auth__RefreshTokenLifetimeDays=7
Auth__DbEncryptionKey=

# Web Search
Search__Provider=SearXng
Search__BraveApiKey=

# Audio Transcriber
TRANSCRIBER__WhisperModel=base
TRANSCRIBER__TimeoutSeconds=1800

# Social Providers (Optional)
LINKEDIN__CLIENTID=
LINKEDIN__CLIENTSECRET=
TWITTER__CLIENTID=
TWITTER__CLIENTSECRET=
METATHREADS__APPID=
METATHREADS__APPSECRET=
EOF

chown deploy:deploy /opt/social-worker/.env
chmod 600 /opt/social-worker/.env
```

---

## 4. Configure GitHub Repository Secrets

In your GitHub repository:
1. Navigate to **Settings** > **Secrets and variables** > **Actions**.
2. Click **New repository secret** and add:
   - `HETZNER_HOST`: `<Your Server Public IPv4>`
   - `HETZNER_USER`: `deploy`
   - `HETZNER_SSH_KEY`: The contents of `/home/deploy/.ssh/id_ed25519` from Step 3.4.
   - `HETZNER_PORT`: `22` *(optional)*

---

## 5. Deployment & Release

### Automatic Deployment (SemVer Release Tag)
Whenever you create and push a release tag matching `v*`:
```bash
git tag v0.1.0
git push origin v0.1.0
```

The GitHub Actions workflow (`.github/workflows/release.yml`) will:
1. Run full test suites across API, Web, and Image Engine.
2. Build container images (`api`, `web`, `transcriber`, `image-engine`) and push them to GitHub Container Registry (GHCR).
3. Connect to your Hetzner VPS over SSH.
4. Sync `docker-compose.prod.yml` and `searxng/settings.yml`.
5. Pull images and restart the stack.
6. Verify `/api/health` on loopback.

### Manual Trigger
You can also trigger a deployment manually at any time via **Actions** > **Release and Deploy** > **Run workflow**.

---

## 6. Verification & Operational Commands

- Open `https://social.plopsaurus.xyz` in your browser.
- Verify health:
  ```bash
  curl -i https://social.plopsaurus.xyz/api/health
  ```

### Useful Maintenance Commands (on Server)

```bash
cd /opt/social-worker

# View container status
docker compose -f docker-compose.prod.yml ps

# Check Cloudflare Tunnel connection logs
docker compose -f docker-compose.prod.yml logs -f tunnel

# Tail all logs
docker compose -f docker-compose.prod.yml logs -f --tail=100

# Tail API logs specifically
docker compose -f docker-compose.prod.yml logs -f api
```

---

## 7. Automated Database Backups & Disaster Recovery

The repository includes a production-ready backup and restore script at `/opt/social-worker/scripts/backup-db.sh`.

### 7.1 Schedule Daily Backups (Cron)
On the server, add a cron job to run the backup daily at 03:00 UTC and retain the last 14 days of backups:
```bash
crontab -e
```
Add the line:
```cron
0 3 * * * /opt/social-worker/scripts/backup-db.sh >> /var/log/social-worker-backup.log 2>&1
```

### 7.2 Manual Backup & Restore Commands
```bash
cd /opt/social-worker

# Create an immediate manual backup
./scripts/backup-db.sh backup

# List all local backups
./scripts/backup-db.sh list

# Restore from a backup file (with safety confirmation)
./scripts/backup-db.sh restore ./backups/db/socialworker_20261019_030000.dump
```

### 7.3 Optional Offsite Backup to Cloudflare R2 ($0 Cost)
To protect against complete server loss (3-2-1 backup strategy):
1. Create a bucket in Cloudflare R2 (e.g. `social-worker-backups`).
2. Install and configure `rclone` on the VPS (`apt install -y rclone && rclone config` with Cloudflare R2 credentials).
3. Add `R2_BUCKET=social-worker-backups` to `/opt/social-worker/.env`.
4. The backup script will automatically upload every daily backup to Cloudflare R2!

