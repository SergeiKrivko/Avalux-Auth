# Стек `auth`: деплой и материалы H-07

Живой стек управляется в Portainer (endpoint `local`, stack `auth`, id 10).
`auth-stack.yml` — зеркало его compose-файла для ревью; изменения применяются
через Stacks → `auth` → Update (или `PUT /api/stacks/10?endpointId=3`).
Значения `${...}` хранятся в переменных стека Portainer и в git не попадают.

## Переменные стека

| Имя | Назначение |
|---|---|
| `DB__NAME`, `DB__USER`, `DB__PASS` | initdb-суперпользователь Postgres (initdb и админские задачи; рантайм им не пользуется) |
| `DB__APP_USER` = `avaluxauth_app`, `DB__APP_PASS` | least-privilege роль рантайма |
| `REDIS__PASSWORD` | пароль Redis (AUTH), 48 hex |
| `Admin__Login`, `Admin__Password` | вход в админку |
| `Api__ApiUrl`, `Security__Issuer`, `Security__AuthApiUrl` | внешние URL |
| `S3__*` | доступ к S3 |
| `Security__RedisCaCertificatePath` | CA для явной проверки TLS Redis (используется сборкой `:master`) |

## Сеть

`auth-postgres` и `auth-redis` подключены только к bridge-сети
`auth_auth-internal` (`internal: true`). `auth` подключён к ней и к внешней
`proxy` (через `proxy` его видит Traefik). Контейнеры на `proxy` не должны
резолвить имена БД/Redis.

## TLS

Том `auth_auth_tls` монтируется в `auth`, `auth-postgres` и `auth-redis` как
`/certs:ro`:

| Файл | Владелец / режим | Назначение |
|---|---|---|
| `ca.crt` | root 644 | корневой сертификат приватного CA |
| `ca-bundle.crt` | root 644 | системные корни + `ca.crt`; нужен `auth` на закреплённой сборке (`SSL_CERT_FILE`) |
| `postgres.crt` | root 644 | серверный сертификат, SAN `auth-postgres`, `localhost`, `127.0.0.1` |
| `postgres.key` | `70:70` 600 | ключ Postgres |
| `redis.crt` | root 644 | серверный сертификат, SAN `auth-redis`, `localhost`, `127.0.0.1` |
| `redis.key` | `999:1000` 600 | ключ Redis |
| `pg_hba.conf` | root 644 | только `local ... trust` и `hostssl ... scram-sha-256` |

Том `auth_auth_tls_ca` хранит `ca.key` (600) и никуда не монтируется.
Сертификаты самоподписанные, выпущены 2026-11-19 сроком на 10 лет (до
2036-11-15). Для перевыпуска — те же SAN и `openssl`, заменить файлы в томе и
перезапустить сервисы. `pg_hba.conf` задаётся через
`-c hba_file=/certs/pg_hba.conf`, поэтому правки `pg_hba.conf` внутри PGDATA
игнорируются.

`SSL_CERT_FILE=/certs/ca-bundle.crt` нужен только закреплённой сборке
`c4c9617`, которая не умеет проверять приватный CA явно. После перевода
стека на `:master` CA проверяется кодом (`Security:RedisCaCertificatePath`),
`SSL_CERT_FILE` можно убрать.

## Роль БД

`db-bootstrap.sql` создаёт роль `avaluxauth_app` и переводит на неё БД, схему
и таблицы. Применён на живом контуре 2026-11-19. Запуск (из окружения, где
есть `DB__PASS` и `DB__APP_PASS`):

```bash
PGPASSWORD="$DB__PASS" psql -h auth-postgres -U "$DB__USER" -d "$DB__NAME" \
  -v ON_ERROR_STOP=1 -f deploy/db-bootstrap.sql
```

`REASSIGN OWNED BY <superuser>` использовать нельзя: системные каталоги
принадлежат initdb-суперпользователю, и команда падает с «required by the
database system» — скрипт переводит только прикладные объекты.

## Закреплённая ревизия и переход на `:master`

`auth` закреплён по digest на сборку `c4c9617` (состояние до H-02/H-04/H-05).
В `:master` уже есть H-05, которая требует PKCE и ломает десктоп-клиенты на
`Avalux.Auth.UserClient` 1.3.2, пока они не обновлены до версии с PKCE
(>= 1.4.0). Миграция `OAuthClientBinding` уже применена к БД (при временной
проверке `:master`), поэтому при следующем деплое `:master` она не выполнится
повторно — переходные шаги ниже нужно сделать вручную. Переводить стек на
`:master` только после обновления клиентов; порядок:

1. Вернуть `image: ghcr.io/sergeikrivko/avalux-auth:master`.
2. Убрать `SSL_CERT_FILE` (CA проверяется через `Security:RedisCaCertificatePath`).
3. Добить `ClientId` у refresh-токенов, выданных закреплённой сборкой, и
   снять временные DEFAULT'ы:

   ```sql
   UPDATE "RefreshTokens" rt
   SET "ClientId" = a."ClientId"
   FROM "Users" u
   JOIN "Applications" a ON a."Id" = u."ApplicationId"
   WHERE u."Id" = rt."UserId" AND rt."ClientId" = '';

   ALTER TABLE "RefreshTokens" ALTER COLUMN "ClientId" DROP DEFAULT;
   ALTER TABLE "RefreshTokens" ALTER COLUMN "CreatedAt" DROP DEFAULT;
   ALTER TABLE "RefreshTokens" ALTER COLUMN "ExpiresAt" DROP DEFAULT;
   ```

   DEFAULT'ы добавлены при откате на старую ревизию: она не знает про эти
   NOT NULL-колонки, появившиеся в миграции `OAuthClientBinding`.

## Проверка

```bash
# снаружи: формы и OIDC
curl -s -o /dev/null -w '%{http_code}\n' https://auth.reportchecker.ru/login
curl -s -o /dev/null -w '%{http_code}\n' https://auth.reportchecker.ru/api/v1/.well-known/jwks.json

# Redis: plaintext закрыт, TLS+AUTH работает
docker exec auth-redis redis-cli -p 6379 ping                      # Connection reset
docker exec auth-redis sh -c 'redis-cli --tls --cacert /certs/ca.crt -h auth-redis -p 6379 -a "$REDIS__PASSWORD" --no-auth-warning ping'  # PONG

# Postgres: рантайм под least-privilege ролью по TLS, без TLS — отказ
docker exec auth-postgres psql -U avaluxauth -d avaluxauth -c \
  "select a.usename, s.ssl from pg_stat_activity a join pg_stat_ssl s on s.pid=a.pid where a.datname='avaluxauth'"

# Изоляция: из контейнера в сети proxy имена БД/Redis не должны резолвиться
docker exec <proxy-container> getent hosts auth-redis auth-postgres  # пусто / ошибка
```
