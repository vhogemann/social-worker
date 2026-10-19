# CI/CD Pipeline & Hetzner Deployment Plan

## Context & Goal

Provide a robust, cost-effective CI/CD pipeline and single-user production deployment for **social-worker** hosted on **Hetzner Cloud** with secure edge ingress via **Cloudflare Tunnel (`cloudflared`)** and containerized orchestration via **Docker Compose**.

---

## Architecture & Specifications

| Component | Target Spec | Details |
| :--- | :--- | :--- |
| **Compute** | Hetzner Cloud CX22 | 2 vCPU, 4 GB RAM, 40 GB NVMe (~€3.79/mo) in EU (Germany/Finland) |
| **OS & Tuning** | Ubuntu 24.04 LTS | 2 GB swapfile safety buffer for Whisper audio inference |
| **Ingress & Security** | Cloudflare Tunnel (`cloudflared`) | Zero open web ports (ports 80/443 closed); outbound-only tunnel to Cloudflare Edge |
| **Domain & SSL** | `social.plopsaurus.xyz` | Cloudflare Edge SSL/TLS, DDoS protection, WAF |
| **Container Registry** | GitHub Container Registry (GHCR) | `ghcr.io/vhogemann/social-worker-{api,web,transcriber,image-engine}` |
| **CI/CD** | GitHub Actions (`release.yml`) | Triggered on SemVer release tags (`v*`) or manual workflow dispatch |

---

## Deliverables

1. **Production Health Endpoint**:
   - Added `GET /api/health` returning `{ status = "healthy", timestamp = ... }` with unit test in `SocialWorker.Api.Tests`.
2. **Cloudflare Tunnel Ingress (`docker-compose.prod.yml`)**:
   - Outbound `cloudflared` container connecting to Cloudflare edge using `CLOUDFLARE_TUNNEL_TOKEN`.
   - Internal routing to `web:80`, which proxies `/api/`, `/.well-known/`, `/oauth/`, and `/mcp/` to `api:8080`.
   - Loopback-only port `127.0.0.1:8100` for server-side health checking.
3. **Production Compose Specification (`docker-compose.prod.yml`)**:
   - Pre-built GHCR images for API, Web, Transcriber, and Image Engine.
   - Internal network isolation for database, sidecars, and cache.
   - Volumes for database, uploads, transcripts, whisper cache, and DP keys.
4. **Automated CI/CD Pipeline (`.github/workflows/release.yml`)**:
   - Test stage: runs API, Web, and Image Engine test suites.
   - Build stage: builds and pushes all 4 images with GHA layer caching.
   - Deploy stage: connects via SSH to Hetzner VPS, syncs config, pulls images, restarts stack, and verifies `/api/health`.
5. **Operational Documentation (`docs/DEPLOYMENT_HETZNER.md`)**:
   - Complete server setup, UFW firewall rules (only SSH allowed), swapfile allocation, Docker installation, deploy user creation, Cloudflare tunnel token configuration, and troubleshooting commands.

---

## Status

`Completed` (Configuration & Pipeline ready; awaiting server provisioning, Cloudflare Tunnel creation, and GitHub Secrets population).
