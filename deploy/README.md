# Стек `auth`: деплой и материалы H-07

Живой стек управляется в Portainer (endpoint `local`, stack `auth`, id 10).
`auth-stack.yml` — зеркало его compose-файла для ревью; изменения применяются
через Stacks → `auth` → Update (или `PUT /api/stacks/10?endpointId=3`).
Значения `${...}` хранятся в переменных стека Portainer и в git не попадают.

> Изменения стека применять только из `deploy/auth-stack.yml` этой ветки: 2026-11-19
> параллельный деплой из устаревшей копии уже снёс `/recover` и `Email__*`.
> Перед Update сверяйте файл с `master`, иначе легко потерять чужие изменения.

## Переменные стека

| Имя | Назначение |
|---|---|
| `DB__NAME`, `DB__USER`, `DB__PASS` | initdb-суперпользователь Postgres (initdb и админские задачи; рантайм им не пользуется) |
| `DB__APP_USER` = `avaluxauth_app`, `DB__APP_PASS` | least-privilege роль рантайма |
| `REDIS__PASSWORD` | пароль Redis (AUTH), 48 hex |
| `Admin__Login`, `Admin__Password` | вход в админку |
| `Api__ApiUrl`, `Security__Issuer`, `Security__AuthApiUrl` | внешние URL |
| `S3__*` | доступ к S3 |
| `S3__Buckets__Avatars` | бакет аватаров |
| `S3__Buckets__Icons` | бакет иконок приложений (загружаются на странице `/app/:id/config`) |
| `Security__RedisCaCertificatePath` | CA для явной проверки TLS Redis (используется сборкой `:master`) |
| `Email__Mode` | `Disabled` (по умолчанию), `Smtp` — боевой, `Log` — только стенд: письма целиком (одноразовые ссылки сброса и подтверждения) пишутся в лог |
| `Email__Host`, `Email__Port`, `Email__Security` | SMTP-сервер, порт (587) и режим TLS: `StartTls`, `SslOnConnect`, `Auto`; из контейнера нужен egress |
| `Email__User`, `Email__Password` | SMTP-аутентификация (для relay может быть пустой); пароль — только переменная стека |
| `Email__FromAddress`, `Email__FromName` | отправитель писем о восстановлении пароля |
| `PasswordRecovery__LinkBaseUrl` | база ссылки из письма; по умолчанию `Api__ApiUrl` |
| `PasswordRecovery__TokenLifetimeMinutes` | срок жизни ссылки, по умолчанию 30 (5–120) |
| `EmailConfirmation__LinkBaseUrl` | база ссылки подтверждения email; по умолчанию `PasswordRecovery__LinkBaseUrl`, затем `Api__ApiUrl` |
| `EmailConfirmation__TokenLifetimeMinutes` | срок жизни ссылки подтверждения, по умолчанию 1440 (24 часа), допустимо 15–10080 |

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

> На 2026-11-19 живой стек уже переведён на `:master`, и в роутер добавлен `/recover`. Раздел ниже описывает исходный план перехода и оставлен для истории; десктоп-клиентам по-прежнему нужен UserClient >= 1.4.0 (PKCE).

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

## Восстановление пароля (R-01)

Самостоятельный сброс пароля включается только явно: `Email__Mode=Disabled`
(по умолчанию) отдаёт на `/api/v1/password/recover` ответ 503, поэтому
обновление стека без SMTP-переменных не ломает вход и не создаёт видимость
работающей почты.

Боевой режим:

1. Заведите ящик/relay и добавьте в переменные стека `Email__Mode=Smtp`,
   `Email__Host`, `Email__Port`, `Email__Security`, `Email__FromAddress`,
   `Email__FromName`, при необходимости `Email__User`/`Email__Password`.
   Контейнеру нужен egress к SMTP-хосту (587/STARTTLS или 465/SSL).
2. `PasswordRecovery__LinkBaseUrl` можно не задавать — используется
   `Api__ApiUrl` (`https://auth.reportchecker.ru`). Заголовки `X-Forwarded-Host`
   для ссылки намеренно не используются.
3. В Traefik-правиле публичного роутера уже есть `PathPrefix(/recover)`;
   без него страница из письма недоступна снаружи.
4. Ссылка ведёт на `/recover#token=...`: токен лежит во фрагменте, не попадает
   в логи прокси и стирается из адресной строки после загрузки страницы.
   Живёт 30 минут (настраивается) и срабатывает один раз.

Стенд без SMTP: `Email__Mode=Log` — письмо целиком (включая ссылку) пишется в
лог приложения. Этот режим нельзя включать на боевом контуре: он кладёт
одноразовые ссылки в логи.

Ограничения: email в аккаунте необязателен (`Passwords.Email`, не более 50
символов); аккаунты без email и аккаунты, созданные только через внешний
провайдер, восстановить нельзя — им доступен вход прежним способом. Адрес можно
подтвердить письмом (R-02), но подтверждение необязательно и на восстановление
не влияет.

## Подтверждение email (R-02)

Регистрация с email ставит в очередь письмо со ссылкой
`/confirm#token=...`; страница предлагает перейти по ней (и тогда вход
завершается автоматически, без повторного пароля) или сразу продолжить в
приложение. Подтверждение необязательно: без SMTP, без адреса или при
пропуске страницы вход работает как раньше, а `Passwords.EmailConfirmedAt`
просто остаётся `NULL`.

Что нужно для включения на живом контуре:

1. `Email__Mode=Smtp` и остальные `Email__*` — та же почта, что использует
   восстановление пароля (R-01). Без рабочего отправителя страница
   подтверждения не показывается.
2. В allowlist публичного роутера `auth` добавить `PathPrefix(/confirm)`:
   новый публичный префикс при fail-closed allowlist иначе отдаёт 404.
   В `deploy/auth-stack.yml` правило и переменные уже обновлены.
3. Образ должен содержать миграцию `EmailConfirmation` (колонка
   `Passwords.EmailConfirmedAt`): она применяется при старте автоматически.
   `EmailConfirmation__LinkBaseUrl` можно не задавать; срок жизни ссылки —
   `EmailConfirmation__TokenLifetimeMinutes` (по умолчанию 24 часа).

Токен одноразовый, в Redis лежит только SHA-256, живёт 24 часа и передаётся во
фрагменте; `POST /api/v1/password/confirm` ограничен 20 попытками на IP за
10 минут. Ссылка после смены адреса в профиле подтверждает только новый ящик
(без входа). Подробности и остаточные риски (login CSRF, PKCE при переходе на
другом устройстве) — в `SECURITY-AUDIT.md`, раздел 12.

## Иконки приложений

Иконка выбирается на странице приложения `/app/:id/config` (кнопки «Загрузить»,
«Заменить», «Удалить»). API нормализует изображение в PNG 256×256 с сохранением
прозрачности и кладёт объект в бакет `S3__Buckets__Icons` под ключом
`{applicationId}/icon.png`; в `Applications.IconUrl` хранится публичный URL
`{Api__ApiUrl}/api/v1/apps/{id}/icon?v=...` (версия меняется при каждой загрузке,
поэтому браузер не показывает старую иконку).

Публичный роутер отдаёт эту иконку анонимно: она нужна на странице входа
(`/login`, `/profile`) и в админке, где собственной сессии у `<img>` нет. Поэтому
в правиле `auth` есть `PathPrefix(/api/v1/apps)` — без него иконки не откроются
снаружи. Идентификатор приложения — случайный GUID, отдаётся только картинка.

Обновляя живой стек, добавьте переменную `S3__Buckets__Icons` (и сам бакет с
правом на запись для ключа доступа `S3__AccessKey`) и `PathPrefix(/api/v1/apps)`
в правило роутера `auth`. При удалении приложения объект иконки удаляется
(иконка-сирота возможна только при недоступности S3 в этот момент).

## Проверка

```bash
# снаружи: формы и OIDC
curl -s -o /dev/null -w '%{http_code}\n' https://auth.reportchecker.ru/login
curl -s -o /dev/null -w '%{http_code}\n' https://auth.reportchecker.ru/recover
curl -s -o /dev/null -w '%{http_code}\n' https://auth.reportchecker.ru/confirm
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
