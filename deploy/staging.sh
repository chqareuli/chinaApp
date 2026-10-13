#!/usr/bin/env bash
# Run as root on the existing /opt/chiniseapp test server.
set -Eeuo pipefail
umask 077
revision=${1:?Pass the full Git commit SHA}
[[ "$revision" =~ ^[a-f0-9]{40}$ ]] || exit 2
base=/opt/chiniseapp
repository=$base/src/frontend
source=$base/src/release-$revision
release=$base/releases/$revision
backup=$base/backups/$revision-$(date -u +%Y%m%dT%H%M%SZ)
exec 9>/run/lock/chiniseapp-deploy.lock
flock -n 9 || { echo 'Another deployment is running'; exit 1; }
sudo -u chiniseapp git -C "$repository" cat-file -e "$revision^{commit}"
[[ ! -e "$source" && ! -e "$release" ]] || { echo 'Release already exists; inspect it before retrying'; exit 1; }
sudo -u chiniseapp git -C "$repository" worktree add --detach "$source" "$revision"
install -d -o chiniseapp -g chiniseapp "$release"
sudo -u chiniseapp dotnet publish "$source/src/Chiniseapp.Api/Chiniseapp.Api.csproj" -c Release -o "$release/backend"
(
  cd "$source/frontend/editorial-panel"
  sudo -u chiniseapp npm ci --no-audit --no-fund
  sudo -u chiniseapp npm run build
  cp -a dist/editorial-panel/browser "$release/frontend"
)
test -f "$release/backend/Chiniseapp.Api.dll"
test -f "$release/frontend/index.html"
install -d -m 700 "$backup"
cp -a "$base/app/backend" "$backup/backend"
cp -a "$base/app/frontend" "$backup/frontend"
install -d -o chiniseapp -g chiniseapp "$base/storage/media"
if [[ -d "$base/app/backend/App_Data/media" && ! -L "$base/app/backend/App_Data/media" ]]; then
  cp -an "$base/app/backend/App_Data/media/." "$base/storage/media/"
fi
tar -czf "$backup/storage.tar.gz" -C "$base" storage
stopped=0
switched=0
recover() {
  code=$?
  trap - ERR
  echo "Deployment failed; backup: $backup"
  if (( switched )); then
    systemctl stop chiniseapp-api || true
    for part in backend frontend; do
      if [[ -d "$backup/previous-$part" ]]; then
        if [[ -e "$base/app/$part" ]]; then mv "$base/app/$part" "$backup/failed-$part"; fi
        cp -a "$backup/$part" "$base/app/$part"
      fi
    done
  fi
  if (( stopped )); then systemctl start chiniseapp-api || true; fi
  echo 'Database backup retained; migrations are not automatically reverted.'
  exit "$code"
}
trap recover ERR
systemctl stop chiniseapp-api
stopped=1
sudo -u postgres pg_dump -Fc chiniseapp > "$backup/database.dump"
pg_restore -l "$backup/database.dump" >/dev/null
systemd-run --wait --pipe --unit="chiniseapp-migrate-${revision:0:12}" \
  -p User=chiniseapp -p WorkingDirectory="$release/backend" \
  -p EnvironmentFile=/etc/chiniseapp/chiniseapp.env \
  /usr/bin/dotnet "$release/backend/Chiniseapp.Api.dll" --migrate-only
cp -a "$release/backend" "$base/app/backend.next"
cp -a "$release/frontend" "$base/app/frontend.next"
mkdir -p "$base/app/backend.next/App_Data"
ln -s "$base/storage/media" "$base/app/backend.next/App_Data/media"
chown -R chiniseapp:chiniseapp "$base/app/backend.next" "$base/app/frontend.next"
switched=1
mv "$base/app/backend" "$backup/previous-backend"
mv "$base/app/frontend" "$backup/previous-frontend"
mv "$base/app/backend.next" "$base/app/backend"
mv "$base/app/frontend.next" "$base/app/frontend"
systemctl start chiniseapp-api
healthy=0
for attempt in $(seq 1 30); do
  if curl --max-time 3 -fsS http://127.0.0.1:5010/health/db; then healthy=1; break; fi
  sleep 1
done
test "$healthy" = 1
curl --max-time 10 -fsS http://127.0.0.1/api/health/db
curl --max-time 10 -fsS http://127.0.0.1/ | grep -o 'main-[^" ]*\.js'
printf '%s\n' "$revision" > "$base/app/deployed-revision"
echo "DEPLOY_SUCCESS $revision; backup: $backup"
trap - ERR
