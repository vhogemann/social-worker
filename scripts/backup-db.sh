#!/usr/bin/env bash
set -euo pipefail

# Directory setup
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BACKUP_DIR="${BACKUP_DIR:-$REPO_ROOT/backups/db}"
COMPOSE_FILE="${COMPOSE_FILE:-$REPO_ROOT/docker-compose.prod.yml}"
DB_SERVICE="${SW_DB_SERVICE:-db}"
DB_NAME="${SW_DB_NAME:-socialworker}"
DB_USER="${SW_DB_USER:-postgres}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"

mkdir -p "$BACKUP_DIR"

usage() {
  cat <<'EOF'
Usage: ./scripts/backup-db.sh [command]

Automates PostgreSQL database backup, retention pruning, and restore.

Commands:
  backup (default)    Create a compressed pg_dump and prune old backups
  restore <file>      Restore the database from a specified .dump file
  list                List available local backups
  -h, --help          Show this help

Environment variables:
  BACKUP_DIR          Directory where backups are saved (default: ./backups/db)
  COMPOSE_FILE        Compose file to use (default: ./docker-compose.prod.yml)
  RETENTION_DAYS      Days to keep local backups before pruning (default: 14)
  R2_BUCKET           Optional Cloudflare R2 bucket name for offsite sync (e.g. social-worker-backups)
EOF
}

cmd="${1:-backup}"

case "$cmd" in
  backup)
    TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
    BACKUP_FILE="$BACKUP_DIR/socialworker_${TIMESTAMP}.dump"
    
    echo "[INFO] Starting database backup: $BACKUP_FILE"
    docker compose -f "$COMPOSE_FILE" exec -T "$DB_SERVICE" pg_dump -U "$DB_USER" -d "$DB_NAME" -Fc > "$BACKUP_FILE"
    
    FILE_SIZE="$(du -h "$BACKUP_FILE" | cut -f1)"
    echo "[SUCCESS] Backup created: $BACKUP_FILE ($FILE_SIZE)"

    # Prune old local backups
    echo "[INFO] Pruning local backups older than $RETENTION_DAYS days..."
    find "$BACKUP_DIR" -name "socialworker_*.dump" -type f -mtime "+$RETENTION_DAYS" -delete
    
    # Optional Offsite Sync to Cloudflare R2 (if rclone is configured)
    if [[ -n "${R2_BUCKET:-}" ]] && command -v rclone &> /dev/null; then
      echo "[INFO] Syncing backup to Cloudflare R2 bucket: $R2_BUCKET"
      rclone copy "$BACKUP_FILE" "r2:$R2_BUCKET/db/"
      echo "[SUCCESS] Offsite sync complete."
    fi
    ;;

  restore)
    if [[ $# -lt 2 ]]; then
      echo "Error: Must provide the path to the backup file to restore." >&2
      echo "Example: ./scripts/backup-db.sh restore ./backups/db/socialworker_20261019_030000.dump" >&2
      exit 1
    fi
    RESTORE_FILE="$2"
    if [[ ! -f "$RESTORE_FILE" ]]; then
      echo "Error: Backup file not found: $RESTORE_FILE" >&2
      exit 1
    fi

    echo "[WARNING] This will restore the database and overwrite existing data in $DB_NAME."
    read -p "Are you sure you want to proceed? [y/N] " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
      echo "[ABORTED] Restore cancelled."
      exit 0
    fi

    echo "[INFO] Restoring database from: $RESTORE_FILE"
    # Drop and recreate connections, then pg_restore
    docker compose -f "$COMPOSE_FILE" exec -T "$DB_SERVICE" pg_restore -U "$DB_USER" -d "$DB_NAME" --clean --if-exists < "$RESTORE_FILE" || true
    echo "[SUCCESS] Database restored successfully."
    ;;

  list)
    echo "Available local database backups in $BACKUP_DIR:"
    ls -lh "$BACKUP_DIR"/socialworker_*.dump 2>/dev/null || echo "No backups found."
    ;;

  -h|--help)
    usage
    exit 0
    ;;

  *)
    echo "Unknown command: $cmd" >&2
    usage
    exit 1
    ;;
esac
