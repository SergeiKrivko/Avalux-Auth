# Аудит безопасности AvaluxAuth

- **Дата:** 2026-11-18 (Europe/Moscow); исправления H-07 и M-01 — 2026-11-19; M-02, M-03 и M-05 — 2026-11-19; L-04, L-05 и L-09 — 2026-11-19.
- **Ревизия:** `ab6731d` (`master`) — аудит; `9c6575c` — код H-07 (валидация TLS Redis по приватному CA); `4a09914` — код H-06 (rate limiting/lockout, сжигание state, политика пароля); `f6651e7` — код M-01 (state/квоты/rate limit для загрузки аватаров, очистка сирот); `6dfe8c0` — код M-02 (учёт `StartsAt` в claim `Subscriptions` и в админ-проекции); `5090e0b` — код M-03 (поколение сессии `Users.SessionVersion`, отзыв всех сессий при смене пароля/линковке, привязка кода авторизации к поколению); `90a23db` — код M-05 (логирование через `ILogger` без секретов и PII, `LogSanitizer` для недоверенного текста); `30c2b78` — код L-04 (границы `page`/`limit`, порядок `Skip`/`Take`, постраничная выдача пользователей); `1425071` — код L-05 (`grant_type=refresh_token`, сохранение refresh-токена, `access_type=offline`); `aa34d17` — код L-09 (`**/keys/` в `.gitignore`, `**/keys` в `.dockerignore`). На живом контуре — образ `c4c9617` (см. H-07, раздел «Порядок деплоя»).
- **Область:** `Api/*` (API, сервисы, провайдеры, DataAccess, S3), `AuthUI`, `UI`, Docker/CI, клиентские библиотеки, живой контур в Portainer (стек `auth`).
- **Статус:** H-01 и H-07 исправлены; H-02, H-04, H-05, H-06, M-01, M-02, M-03, M-05, L-04 и L-05 исправлены в коде, но ещё не задеплоены на живой контур; L-09 исправлен (только ignore-файлы, деплой не требуется); H-03 открыт (пакеты и загрузка закрыты, остаётся ограничение декодирования).
- **Методика:** статический разбор кода и миграций, `dotnet list package --vulnerable --include-transitive`, `npm audit`, сверка с OSV/NuGet, безопасные проверки живого контура (GET/негативные POST без побочных эффектов, один инертный XSS-payload).
- **Ограничения:** не выполнялись брутфорс, разрушительные действия, массовое сканирование и выгрузка секретов; полноценный пентест бизнес-логики не проводился.

---

## 1. Резюме

На момент аудита: **7 High**, **12 Medium** и **11 Low/hardening** по API/инфраструктуре, плюс 3 пункта по клиентским библиотекам (C-01…C-03) и зависимости.

Наиболее опасные классы:

1. **H-01** — админ-API был доступен из интернета в обход VPN из-за регистрозависимого `PathPrefix` в Traefik и регистронезависимой маршрутизации ASP.NET. **Исправлено 2026-11-18.**
2. **H-02** — отражённый XSS на домене авторизации (`auth.reportchecker.ru`), позволяющий фишинговать пароли на легитимном origin. **Исправлено в коде 2026-11-18 (ждёт деплоя).**
3. **H-03** — уязвимый ImageSharp 3.1.12 на анонимной загрузке изображений (удалённый крэш/DoS, TIFF decoder).
4. **H-04** — отзыв service-account токенов не работал: JWT жил до `exp` даже после удаления токена в админке. **Исправлено в коде 2026-11-18 (ждёт деплоя).**
5. **H-05** — коды авторизации и refresh-токены не привязаны к клиенту, client secret в проде отключён, PKCE только заявлен. **Исправлено в коде 2026-11-18 (ждёт деплоя).**
6. **H-06** — не было rate limiting/lockout; state переиспользовался при неудачном входе, логины перечислялись. **Исправлено в коде 2026-11-19 (ждёт деплоя).**
7. **H-07** — Redis без пароля и Postgres на общей сети `proxy`. **Исправлено 2026-11-19** (internal-сеть, auth+TLS, least-privilege DB-роль).

### Сводная таблица

| ID | Severity | Проблема | Статус |
|---|---|---|---|
| H-01 | High | Админ-API доступен из интернета в обход VPN (Traefik + регистр) | **Исправлено** |
| H-02 | High | Отражённый XSS в HTML-странице ошибки авторизации | **Исправлено в коде** |
| H-03 | High | Уязвимый ImageSharp + анонимная загрузка/декодирование изображений | Открыто |
| H-04 | High | Отзыв service-account токенов не проверяется при аутентификации | **Исправлено в коде** |
| H-05 | High | OAuth: нет привязки кода/refresh к клиенту, client secret отключён, PKCE фиктивный | **Исправлено в коде** |
| H-06 | High | Нет rate limiting/lockout, переиспользование state, перечисление логинов | **Исправлено в коде** |
| H-07 | High | Redis без auth и Postgres на общей сети `proxy` | **Исправлено** |
| M-01 | Medium | Анонимная запись в S3 без квот и очистки | **Исправлено в коде** |
| M-02 | Medium | Будущие подписки выдаются сразу (`StartsAt` не учитывается) | **Исправлено в коде** |
| M-03 | Medium | Смена пароля не инвалидирует сессии; refresh-токены бессрочные | **Исправлено в коде** |
| M-04 | Medium | Ротация signing key не инвалидирует старые ключи/JWKS | Открыто |
| M-05 | Medium | Секреты и PII в логах (`Console.WriteLine`) | **Исправлено в коде** |
| M-06 | Medium | Нет security-заголовков (CSP/HSTS/frame/nosniff/referrer) | Открыто |
| M-07 | Medium | DataProtection keys: каталог 777, ключи без шифрования at rest | Открыто |
| M-08 | Medium | Вербозные 500 и необработанные исключения (state, color) | Открыто |
| M-09 | Medium | CORS по умолчанию `AllowAnyOrigin` | Открыто |
| M-10 | Medium | Небезопасные redirect URI в проде (Vercel preview, localhost) | Открыто |
| M-11 | Medium | Admin login: fail-open на пустом пароле, неконстантное сравнение | Частично (H-06) |
| M-12 | Medium | Нет unique index на `Passwords.Login`; гонка при регистрации | Открыто |
| L-01 | Low | JWT: `ValidateAudience=false`, `RequireHttpsMetadata=false` | Открыто |
| L-02 | Low | Доверие `Host`/`X-Forwarded-*`, `AllowedHosts: "*"` | Открыто |
| L-03 | Low | `TokenRepository.GetTokenByIdAsync` возвращает только удалённые токены | **Исправлено (H-04)** |
| L-04 | Low | Пагинация: неверный порядок `Take/Skip`, нет верхней границы `limit` | **Исправлено в коде** |
| L-05 | Low | Google refresh отправляет `grant_type=authorization_code` | **Исправлено в коде** |
| L-06 | Low | `ApiClient.GetAccessTokenAsync` игнорирует provider | Открыто |
| L-07 | Low | Приложение работает под суперпользователем Postgres | **Исправлено (H-07)** |
| L-08 | Low | CI: лишние права, теги вместо SHA/digest, нет scan/SBOM | Открыто |
| L-09 | Low | `keys/` не в `.gitignore`/`.dockerignore` | **Исправлено** |
| L-10 | Low | Нет аудит-лога, healthcheck и resource limits у `auth` | Открыто |
| L-11 | Low | Avatar GET: неограниченный `text` и `colorIndex` → CPU/500 | Открыто |
| C-01 | Medium | UserClient: command injection на Windows, `provider` без экранирования | Открыто |
| C-02 | Medium | UserClient: нет `state`/PKCE → login CSRF для installed-приложений | **Исправлено в коде (H-05)** |
| C-03 | Low | TestCli: client secret в argv, credentials в plaintext | Открыто |

---

## 2. High

### H-01. Админ-API доступен из интернета в обход VPN — ИСПРАВЛЕНО

**Где:** правило публичного роутера в Portainer-стеке `auth`; маршрутизация — [Program.cs:137](Api/AvaluxAuth.Api/Program.cs#L137), [AdminController.cs](Api/AvaluxAuth.Api/Controllers/AdminController.cs).

**Причина.** Публичный роутер Traefik исключал админку через `!PathPrefix('/api/v1/admin')`. `PathPrefix` в Traefik регистрозависим, а маршрутизация ASP.NET — регистронезависима. Запрос `/api/v1/ADMIN/...` не попадал под исключение, проходил на публичный хост и маршрутизировался в `AdminController`. VPN-middleware `internal@file` применялась только к роутеру `auth-admin`.

**Подтверждение (до фикса):**

| Запрос | Результат |
|---|---|
| `GET /api/v1/admin/test` | 404 (исключение сработало) |
| `GET /api/v1/ADMIN/test` | **401** (запрос дошёл до приложения) |
| `POST /api/v1/ADMIN/login` | **401** на неверные данные — эндпоинт обрабатывал учётные данные |

**Влияние.** Вся админка (приложения, client secrets, service-account токены, ротация ключей) была доступна с интернета. Авторизация всё ещё требовалась (cookie), но единственный барьер — пароль без rate limit/lockout/MFA.

**Исправление (применено 2026-11-18).** Публичный роутер переведён с чёрного списка на **fail-closed allowlist** + регистронезависимое исключение админки:

```text
Host(`auth.reportchecker.ru`)
&& !PathRegexp(`(?i)^/api/v1/admin`)
&& (PathPrefix(`/api/v1/auth`)
 || PathPrefix(`/api/v1/password`)
 || PathPrefix(`/api/v1/avatar`)
 || PathPrefix(`/api/v1/service`)
 || PathPrefix(`/api/v1/.well-known`)
 || PathPrefix(`/static`)
 || PathPrefix(`/login`)
 || PathPrefix(`/profile`)
 || PathPrefix(`/auth`))
```

**Проверка после фикса:**

| Проверка | Результат |
|---|---|
| Все 32 регистровых варианта `admin` | 404 |
| `%61dmin`, `ad%6din`, `admin%2F`, `admin.`, `admin;`, `//`, `..`, `%2e%2e%2f` | 404 |
| `/api/v1/.well-known/*`, `/login`, `/profile`, `/static/*`, `/api/v1/avatar` | 200 |
| `POST /api/v1/avatar` без файла | 400 (публичный, как и было) |
| `/api/v1/service/users` без токена | 401 (публичный SA, как и было) |
| `/api/v1/password/*`, `/api/v1/auth/token` | 400 (маршрутизируются) |
| `auth-admin.reportchecker.ru/api/v1/admin/test` без VPN | 403 (VPN-middleware не тронута) |

**Остаточный риск.** Приложение по-прежнему отдаёт админ-маршруты любому, кто достучится до контейнера напрямую (в обход Traefik). Defense in depth: проверка `Host` в приложении или отдельный listener/порт для админки.

**Операционное примечание.** Фикс живёт в Portainer-стеке `auth`, не в git. Публичный API теперь allowlist: новый публичный префикс нужно явно добавить в правило Traefik, иначе будет 404 (осознанный fail-closed). Резервные копии конфигурации: `/workspace/auth-stack-after.yml` (применённая), `/workspace/auth-stack-before.yml` (для отката).

---

### H-02. Отражённый XSS на домене авторизации — ИСПРАВЛЕНО В КОДЕ

**Где:** [AuthController.cs:225](Api/AvaluxAuth.Api/Controllers/AuthController.cs#L225) — текст исключения вставляется в HTML без кодирования; [GitLabAuthProvider.cs:61](Api/AvaluxAuth.Providers/GitLabAuthProvider.cs#L61) и [MicrosoftAuthProvider.cs:62](Api/AvaluxAuth.Providers/MicrosoftAuthProvider.cs#L62) включают `error_description` из query в исключение.

**Сценарий.** Анонимно создать state через `/auth/authorize` (нужен публичный `client_id` и зарегистрированный `redirect_uri`), затем открыть callback с `error_description=<img src=x onerror=...>`. Payload возвращается в HTML как есть, статус 200. Страница находится на том же origin, что и форма логина/пароля, поэтому XSS позволяет подменить форму и украсть введённые пароли, а также выполнять same-origin запросы. Отсутствие CSP усиливает атаку.

**Подтверждение:** да, инертным payload'ом на живом контуре.

**Исправление (применено 2026-11-18).** [AuthController.cs](Api/AvaluxAuth.Api/Controllers/AuthController.cs) `ErrorHtml` больше не подставляет в разметку ни `ex.Message`, ни `InnerException`, ни значения из query (включая провайдерский `error_description`). Страница ошибки статическая: в неё попадает только `HttpContext.TraceIdentifier`, прошедший через `HtmlEncoder.Default.Encode`. Детали исключения пишутся в `ILogger` (`LogWarning` с trace id и путём) и в ответ не возвращаются — это заодно закрывает вербозность ошибок авторизации. Ответ отдаётся с кодом 400 и заголовками:

```text
Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'
X-Content-Type-Options: nosniff
Referrer-Policy: no-referrer
Cache-Control: no-store
```

**Проверка после фикса** (локальный запуск исправленной сборки с dev Postgres/Redis):

| Проверка | Результат |
|---|---|
| `/api/v1/auth/authorize` с несуществующим `client_id` | 400, generic-страница, текста `Client not found` нет |
| `/api/v1/auth/gitlab/callback?error_description=<img src=x onerror=alert(1)>` (валидный state) | 400, payload отсутствует в HTML; exception с payload — только в логе |
| Тела ответов при разных ошибках | идентичны, отличается только trace id |
| CSP / nosniff / referrer-policy / cache-control | присутствуют |

**Деплой.** Изменение закоммичено в `master` (`501a9da`), CI пересобрал `ghcr.io/sergeikrivko/avalux-auth:master`. На момент записи живой контур всё ещё работает на старом образе (`State not found` отражается, статус 200) — нужен re-pull + redeploy стека `auth` в Portainer (`pull_policy` не задан, обычный redeploy образ не обновит).

**Остаточный риск.** Глобальных security-заголовков по-прежнему нет (M-06) — защищена только страница ошибки. Сырой `error_description` попадает не в разметку, а в лог; **исправлено вместе с M-05 (`90a23db`)**: `error`/`error_description` и тело ответа провайдера проходят через `LogSanitizer`, поэтому перевод строки в payload не подделывает строку лога.

---

### H-03. Уязвимый ImageSharp на анонимной загрузке изображений

**Где:** [AvaluxAuth.Services.csproj:22](Api/AvaluxAuth.Services/AvaluxAuth.Services.csproj#L22) — `SixLabors.ImageSharp 3.1.12`; [AvatarController.cs:22-34](Api/AvaluxAuth.Api/Controllers/AvatarController.cs#L22-L34) — `[Authorize]` отсутствует; [ImageService.cs:111-113](Api/AvaluxAuth.Services/ImageService.cs#L111-L113) — `Image.LoadAsync` на пользовательском потоке.

**Уязвимости 3.1.12 (OSV/NuGet на 2026-11-18):**

- **GHSA-v76p-62qx-wwq2** (high): tiled fax TIFF — heap OOB write в декодере; fixed **3.2.0**;
- **GHSA-wmxv-xphr-5c9g** (moderate): BigTIFF IFD count — зависание декодера; fixed 3.2.0;
- **GHSA-j3p4-wp97-rph4 / j9gm-c75j-xc9q / jjfr-hcj7-qf5w** (high): HistogramEqualization и TIFF-энкодеры; fixed 3.2.0. В текущем коде не задействованы, но пакет уязвим.

Декодер TIFF достижим **без аутентификации** через `POST /api/v1/avatar`; лимит 10 МБ не защищает от decompression bomb и крафтовых TIFF. Даже «только крэш» с `restart: always` даёт удалённый DoS.

**Исправление.** Обновить `SixLabors.ImageSharp` до **3.2.0**, `SixLabors.ImageSharp.Drawing` до **2.2.0** (зависит от 3.2.0). Ограничить пиксели/размеры (`DecoderOptions`, проверка `Width*Height`). Требовать короткоживущий upload-токен/state для загрузки, rate limit, квоты, очистку сиротских аватаров и bucket lifecycle.

**Обновление (2026-11-19).** Пакеты в `:master` уже 3.2.0/2.2.0 (`5146349`, проверено по `project.assets.json`). Загрузочная часть — state, rate limit, квоты и очистка сирот — реализована в **M-01** (`f6651e7`). Открытым по H-03 остаётся ограничение декодирования (пиксели/размеры, `DecoderOptions`).

---

### H-04. Отзыв service-account токенов не работает — ИСПРАВЛЕНО В КОДЕ

**Где:** [Program.cs:70-88](Api/AvaluxAuth.Api/Program.cs#L70-L88) — JwtBearer не проверял `TokenId`/отзыв; [TokenService.cs:44-76](Api/AvaluxAuth.Services/TokenService.cs#L44-L76) — `IsRevokedAsync` нигде не вызывался; [TokenRepository.cs:13](Api/AvaluxAuth.DataAccess/Repositories/TokenRepository.cs#L13) — фильтр `DeletedAt != null` (возвращает только удалённые токены, логика хрупкая); [TokenController.cs](Api/AvaluxAuth.Api/Controllers/TokenController.cs).

`DELETE /api/v1/admin/apps/{id}/tokens/{tokenId}` ставил `DeletedAt`, но подписанный JWT продолжал приниматься до `exp` (который админ мог поставить на годы). Права читались из claims токена, а не из БД. UI писал «отозвать токен» — это вводило в заблуждение.

**Исправление (применено 2026-11-18).** Отзыв проверяется на каждом запросе с JWT:

1. [Program.cs:96](Api/AvaluxAuth.Api/Program.cs#L96) — `JwtBearerEvents.OnTokenValidated`: если в токене есть claim `TokenId`, вызывается `ITokenService.IsRevokedAsync`. Отозванный или неизвестный токен отклоняется через `context.Fail` (401). Любая ошибка проверки (БД/Redis) тоже отклоняет токен — fail-closed; причина пишется в `ILogger`, наружу не отдаётся. Пользовательские access-токены `TokenId` не содержат и пропускаются без изменений.
2. [TokenService.cs:44](Api/AvaluxAuth.Services/TokenService.cs#L44) — `IsRevokedAsync` сначала проверяет Redis-маркер `RevokedServiceAccountToken-<tokenId>`, при промахе — БД (источник истины): отсутствие записи, `DeletedAt != null` или `ExpiresAt <= UtcNow` означают «отозван»; отрицательный результат кэшируется на сутки. [RevokeTokenAsync](Api/AvaluxAuth.Services/TokenService.cs#L65) после soft delete сразу ставит Redis-маркер (в прежней версии он, наоборот, удалялся из set, а set рос без TTL). [VerifyPermissionAsync](Api/AvaluxAuth.Services/TokenService.cs#L36) теперь проверяет только активный неистёкший токен.
3. [TokenRepository.cs:13](Api/AvaluxAuth.DataAccess/Repositories/TokenRepository.cs#L13) — `GetTokenByIdAsync` больше не фильтрует по `DeletedAt != null` (это был баг L-03): отзыв можно отличить от неизвестного токена, а админский GET по id отдаёт и отозванный токен.

**Проверка** (локальная сборка, dev Postgres/Redis, стек `auth-h04-dev`):

| Проверка | Результат |
|---|---|
| `GET /api/v1/service/users` с валидным service-account JWT | 200 |
| тот же JWT после `DELETE /api/v1/admin/apps/{id}/tokens/{tokenId}` | **401**, `WWW-Authenticate: Bearer error="invalid_token"` |
| БД `Tokens.DeletedAt` = not null; Redis `RevokedServiceAccountToken-<id>` | установлены (TTL ~86400 c) |
| `FLUSHDB` Redis, затем тот же отозванный JWT | **401** (проверка по БД), маркер пересоздан; валидный JWT — 200 |
| Redis остановлен, валидный JWT | **401** (fail-closed), после рестарта — 200 |
| сборка до фикса (`5146349`) с тем же отозванным JWT | **200** — уязвимость воспроизведена |
| в логе при отказе | `Rejected revoked service account token: tokenId=…` |

**Деплой.** Изменение закоммичено в `master` (`a291b5f`); CI пересобрал `ghcr.io/sergeikrivko/avalux-auth:master` (run `44306319530`, success). Для применения на живом контуре нужен re-pull + redeploy стека `auth` (`pull_policy` не задан, обычный redeploy образ не обновит); миграций нет. Активные выданные токены продолжат работать, soft-deleted — перестанут.

**Остаточный риск.** Пользовательские access-токены (1 час) не имеют `jti`/серверной записи; их отзыв сделан отдельно в M-03 (поколение сессии), здесь — вне объёма H-04. Срок жизни service-account токена задаётся админом без верхней границы; отзыв теперь работает, но украденный токен остаётся валидным до отзыва/истечения. Проверка добавляет один запрос к БД на service-account запрос при промахе кэша; ротация ключей/JWKS — M-04.

---

### H-05. OAuth: коды и refresh-токены не привязаны к клиенту, client secret отключён, PKCE фиктивный — ИСПРАВЛЕНО В КОДЕ

**Где (на момент аудита):**

- [AuthController.cs:80-101](Api/AvaluxAuth.Api/Controllers/AuthController.cs#L80-L101) — проверка секрета только если `Security:RequireClientSecret != null`; на момент аудита переменная в контейнере `auth` отсутствовала;
- [AuthController.cs:115-124](Api/AvaluxAuth.Api/Controllers/AuthController.cs#L115-L124) — `/auth/refresh` не проверяет client secret вообще;
- [AuthCode.cs:3-9](Api/AvaluxAuth.Models/AuthCode.cs#L3-L9) — у кода нет `ClientId`, `RedirectUri`, `CodeChallenge`;
- [AuthorizationService.cs:66-71](Api/AvaluxAuth.Services/AuthorizationService.cs#L66-L71) — `client_id`/`redirect_uri` при обмене кода игнорируются;
- [RefreshTokenEntity.cs:5-11](Api/AvaluxAuth.DataAccess/Entities/RefreshTokenEntity.cs#L5-L11) — у refresh-токена нет ни клиента, ни срока жизни;
- [OpenIdConfigurationResponse.cs:24-25](Api/AvaluxAuth.Api/Schemas/OpenIdConfigurationResponse.cs#L24-L25) — заявлен `S256`, но PKCE нигде не реализован.

**Влияние.** Любой, кто получил код или refresh-токен (referer, логи, локальный слушатель, скомпрометированный redirect-домен), обменивает его без аутентификации клиента. Зарегистрированные redirect URI включают `https://report-checker.vercel.app/auth/callback`, `http://localhost:4200/...`, `http://localhost:14872` — при утрате/сквотинге preview-домена это захват аккаунтов.

**Исправление (2026-11-18, ждёт деплоя).**

1. **PKCE S256 реализован и обязателен для публичных клиентов.** `/api/v1/auth/authorize` принимает `code_challenge` + `code_challenge_method=S256`, проверяет формат (unpadded base64url, 43 символа) и сохраняет challenge в `AuthorizationState`. `plain` и отсутствие challenge у публичного клиента отклоняются. `/api/v1/auth/token` требует `code_verifier`, проверяет `BASE64URL(SHA256(verifier)) == challenge` в постоянном времени; код потребляется на каждой попытке, так что подбор verifier невозможен. Проверка — [Pkce.cs](Api/AvaluxAuth.Utils/Pkce.cs).
2. **Код авторизации привязан к клиенту.** В `AuthCode` добавлены `ClientId`, `RedirectUri`, `CodeChallenge`, `CodeChallengeMethod` (и в `AuthorizationState`). Обмен возможен только при совпадении `client_id`, точном совпадении `redirect_uri` и (если был challenge) валидном `code_verifier`. Срок жизни кода снижен с 1 часа до 10 минут; код одноразовый.
3. **Refresh-токен привязан к клиенту и имеет абсолютный срок.** В `RefreshTokens` добавлены `ClientId`, `CreatedAt`, `ExpiresAt`; `/auth/token` и `/auth/refresh` требуют `client_id`, а `/auth/revoke` отзывает только токены своего клиента. Срок жизни — 30 дней по умолчанию (`Security:RefreshTokenLifetimeDays`); ротация токена срок **не** продлевает. Просроченный токен удаляется и не принимается.
4. **Client authentication стала per-application.** Глобальный переключатель `Security:RequireClientSecret` удалён. В `Applications` добавлен `RequireClientSecret`: конфиденциальный клиент обязан прислать точный `client_secret` (сравнение `CryptographicOperations.FixedTimeEquals`), публичный (SPA, десктоп) аутентифицируется через PKCE. В админке — переключатель «Конфиденциальный клиент». Неизвестный `client_id` → `401 invalid_client`; неизвестный `grant_type` → `400 unsupported_grant_type`; ошибки токен-эндпоинта отдаются в формате RFC 6749 (`{"error": ...}`) с `Cache-Control: no-store`.
5. **Redirect URI.** Проверка осталась точным сравнением со списком; дополнительно запрещены fragment'ы. **`http://localhost:*` сохранены осознанно:** это loopback-redirect десктопных приложений (ReportChecker Studio/CLI/VS Code), которые не могут хранить секрет. Их защищает обязательный PKCE, а не удаление redirect URI. Гигиена списка redirect URI (M-10) остаётся отдельной задачей.
6. **Discovery.** `openid-configuration` теперь также публикует `token_endpoint_auth_methods_supported: ["client_secret_post", "none"]`; `code_challenge_methods_supported: ["S256"]` соответствует реальному поведению.

**Миграция.** `20261118161610_OAuthClientBinding`:

- добавляет `Applications.RequireClientSecret` (default `true`);
- добавляет `RefreshTokens.ClientId/CreatedAt/ExpiresAt`; существующим токенам проставляется клиент пользователя и абсолютный срок +30 дней (сессии не рвутся, но перестают быть бессрочными);
- помечает публичными (`RequireClientSecret = false`) приложения, у которых есть loopback-redirect (`localhost`/`127.0.0.1`/`[::1]`) — то есть десктоп-клиенты, для которых PKCE обязателен. Применяется автоматически при старте API.

**Проверка.** На одноразовом стенде `auth-dev` (Postgres 17 + Redis 7, отдельные от прода) поднят API с этой веткой; прогнан сквозной сценарий с провайдером «логин и пароль» — **30/30 проверок**:

| Проверка | Результат |
|---|---|
| `/authorize` публичного клиента без PKCE / `plain` / битый challenge | 400 |
| `/authorize` с S256 → login → signup/signin → код | 302/200 |
| Обмен кода без verifier / с неверным verifier | 400 `invalid_grant` |
| Повторный обмен того же кода | 400 |
| Код, выданный другому `client_id` / с другим `redirect_uri` | 400 |
| Корректный обмен | 200 + `access_token`/`refresh_token`/`id_token` |
| Refresh с чужим `client_id`; старый refresh после ротации | 400 |
| Revoke чужим клиентом; своим; refresh после revoke | 400/200/400 |
| `/auth/refresh` без `client_id` | 400 |
| Конфиденциальный клиент: без секрета / неверный секрет / верный секрет | 401/401/200 |
| Конфиденциальный клиент с PKCE и неверным verifier | 400 |
| `unsupported_grant_type`, отсутствующий `code`, неизвестный `client_id` | 400/400/401 |

Прод-контур и его БД не затрагивались.

**Совместимость клиентов (важно для порядка деплоя).**

- **ReportChecker web** (`angular-oauth2-oidc` 20) уже отправляет `code_challenge_method=S256` и `code_verifier` — изменений не требует (dummy `client_secret` для публичного клиента теперь просто игнорируется).
- **`Avalux.Auth.UserClient` 1.4.0** (в этом репозитории): добавлены PKCE S256, `state` с проверкой в loopback-сервере, `redirect_uri` при обмене. Версия пакета поднята; пакет успешно собирается в CI. Workflow `user-client.yml` переведён на NuGet Trusted Publishing (OIDC, долгоживущий `NUGET_API_KEY` больше не используется). **Публикация 1.4.0 пока не завершена:** OIDC-обмен проходит успешно, но nuget.org отклоняет push с 403 «does not have permission to access the specified package» — нужно проверить package scopes/owner политики Trusted Publishing (репозиторий и workflow уже совпадают, иначе обмен токена не прошёл бы).
- **ReportChecker VS Code / Studio / CLI**: используют собственный `AuthApi` или `Avalux.Auth.UserClient` 1.3.2. После деплоя сервера их нужно перевести на PKCE / `Avalux.Auth.UserClient >= 1.4.0`, иначе `/authorize` вернёт ошибку «PKCE code_challenge is required». Деплой сервера выполнять после обновления клиентов.
- `AvaluxAuth.TestCli` получает PKCE автоматически через обновлённый UserClient.

**Остаточный риск.**

- Client secrets по-прежнему хранятся в БД в открытом виде (вне объёма H-05; для конфиденциальных клиентов стоит хешировать).
- PKCE не защищает от кражи refresh-токена у публичного клиента: его привязка к `client_id` слабая (client_id публичен). Мера — ротация, абсолютный срок 30 дней и revoke; полноценно решается только client authentication для конфиденциальных клиентов.
- Переиспользование `state` при неудачном входе, rate limiting и lockout — H-06 (не входили в H-05).
- ~~Смена пароля не инвалидирует refresh-токены~~ — **исправлено в M-03** (`5090e0b`).

---

### H-06. Rate limiting/lockout, переиспользование state, перечисление логинов — ИСПРАВЛЕНО В КОДЕ

**Где:** [Program.cs](Api/AvaluxAuth.Api/Program.cs) — `AddRateLimiter` отсутствовал; [PasswordController.cs](Api/AvaluxAuth.Api/Controllers/PasswordController.cs) — `GetStateAsync` (не `TakeStateAsync`), state удалялся только при успехе; [AdminController.cs](Api/AvaluxAuth.Api/Controllers/AdminController.cs) — вход без задержек/блокировок; [PasswordService.cs](Api/AvaluxAuth.Services/PasswordService.cs) — серверной политики пароля нет.

**Причина.** Один валидный state давал час неограниченных попыток входа. Плюс 409 «Account already exists» и тайминг BCrypt (для несуществующего логина проверка не выполнялась) позволяли перечислять логины. BCrypt на 2 vCPU — удобный CPU-DoS. Админ-логин был доступен из интернета (H-01).

**Что сделано (2026-11-19, коммит `4a09914`).**

- **Rate limiting по IP.** `AddRateLimiter` + `[EnableRateLimiting]`:
  - `AuthEndpoints` — 30 запросов/мин на клиентский IP для `/password/signin` и `/password/signup`;
  - `AdminLogin` — 10 запросов/5 мин на IP для `/admin/login`.
  При отказе — `429` с `Retry-After` и JSON-телом. Клиентский IP берётся из правого (последнего) элемента `X-Forwarded-For`, если соединение пришло из приватной сети (API публикуется только через Traefik), иначе — из `RemoteIpAddress` ([Extensions.cs](Api/AvaluxAuth.Api/Extensions.cs)).
- **Lockout в Redis.** [RedisLoginThrottle.cs](Api/AvaluxAuth.Services/RedisLoginThrottle.cs) (интерфейс `ILoginThrottle`): счётчик неудач по ключу `scope + IP + SHA-256(login)`; после 5 неудач — блокировка 30 с, далее экспоненциально 60/120/… с потолком 15 мин и окном 15 мин; успешный вход сбрасывает счётчик. Скоупы: `password-signin`, `password-signup`, `admin-login`. Логин в ключах Redis и в логах — только в виде хеша.
- **Ограничение попыток на state.** `IStateRepository.RegisterFailureAsync` инкрементит счётчик `AuthorizationState-{state}-failed-attempts` (TTL 1 ч); после 10 неудач state удаляется вместе со счётчиком. `DeleteStateAsync`/`TakeStateAsync` удаляют и счётчик ([RedisStateRepository.cs](Api/AvaluxAuth.Services/RedisStateRepository.cs)).
- **Единый ответ и постоянное время.** `PasswordService.VerifyPasswordAsync` теперь всегда выполняет `BCrypt.Verify`: для несуществующего логина — против заранее посчитанного dummy-хеша; битый хеш в БД трактуется как неверный пароль, а не 500. Ответ на неверные данные — всегда `401 Wrong login or password`.
- **Админ-логин.** Сравнение логина/пароля — SHA-256 + `CryptographicOperations.FixedTimeEquals` (обе проверки выполняются всегда, без короткого замыкания); при незаданном/пустом `Admin:Login`/`Admin:Password` вход запрещён (fail-closed, частично M-11); неудачи считаются в Redis и логируются.
- **Серверная политика пароля.** [PasswordPolicy.cs](Api/AvaluxAuth.Services/PasswordPolicy.cs): минимум 8 символов, максимум 72 байта UTF-8 (BCrypt молча обрезает более длинные). Проверяется на signup и при смене пароля; дублируется в `PasswordService` как defense in depth.
- **Аудит-лог.** Неудачные входы пишутся через `ILogger` с IP, скоупом, хешем логина и числом попыток по state (без пароля и без открытого логина).
- **Побочный баг.** `RedisStateRepository.GetStateAsync` на отсутствующем ключе выбрасывал `JsonException` и давал 500 вместо 401; теперь возвращает `null`. Без этого «сожжённый» state отвечал бы 500.
- **AuthUI.** На 429 показывается «Слишком много попыток. Подождите немного и попробуйте ещё раз.»

**Проверка (2026-11-19, локально).**

- `dotnet build AvaluxAuth.slnx` — 0 ошибок (только прежние предупреждения).
- Интеграционный прогон против `redis:7-alpine`: пороги 5→30 с, 6→60 с, 7→120 с, потолок 15 мин; ключ нормализует регистр/пробелы логина и IP; reset удаляет ключи; счётчик state инкрементится, имеет TTL, удаляется вместе со state; `GetStateAsync("missing")` → `null`.
- Прогон минимального ASP.NET-пайплайна с той же регистрацией лимитера: первые 30 запросов → 200, следующие → 429 + `Retry-After: 60` + JSON; разные `X-Forwarded-For` попадают в разные корзины; админ-политика — 10 запросов, 11-й → 429.
- На живом контуре не проверялось: фикс ещё не задеплоен (образ закреплён на `c4c9617`).

**Остаточный риск.**

- **MFA для админа не реализована.** Требует TOTP/WebAuthn и изменений в админ-панели; вынести отдельной задачей. Сейчас админ-вход защищён VPN (H-01), rate limit/lockout и константным сравнением.
- **Signup остаётся оракулом существования логина** (409): полноценное закрытие требует e-mail-подтверждения или асинхронной регистрации. Сейчас перечисление ограничено rate limit, per-(IP,login) lockout и сжиганием state.
- **Распределённый перебор.** Per-IP лимиты обходятся ботнетом; per-(IP,login) lockout не позволяет заблокировать чужой аккаунт с другого IP, но и не защищает от распределённого перебора одного логина. Для этого нужен глобальный счётчик по логину с CAPTCHA/уведомлениями.
- **ASP.NET-лимитер — per-process.** При нескольких репликах `auth` лимиты по IP нужно вынести в Redis (сейчас реплика одна, Redis-lockout общий).
- **Доверие `X-Forwarded-For`** — только для приватного peer'а; при прямом доступе к контейнеру изнутри сети `proxy` заголовок можно подделать и сменить корзину (L-02; defense in depth — `ForwardedHeaders` с `KnownNetworks`).
- **Аудит-лог** пишется только в stdout с ротацией 1m (L-10), без централизованного хранения.

---

### H-07. Redis без пароля и Postgres на общей сети `proxy` — ИСПРАВЛЕНО

**Где:** Portainer-стек `auth`: `auth-redis` без `requirepass`, оба сервиса в сети `proxy`; строки подключения — [Program.cs:54-82](Api/AvaluxAuth.Api/Program.cs#L54-L82).

Любой контейнер на `proxy` (ReportChecker, dsh и т.д.) может читать/писать Redis. Через **запись** `AuthorizationState` можно подменить `LinkUserId` на жертву и затем через `/password/signup?state=...` привязать к ней свой пароль; через **чтение** — украсть `link_code` или auth code и обменять на токены (тем более без client secret). Postgres тоже доступен на `proxy`, TLS нет.

**Что сделано (2026-11-19).**

- **Сеть.** Добавлена bridge-сеть `auth_auth-internal` с `internal: true`. `auth-postgres` и `auth-redis` подключены только к ней; `auth` — к ней и к `proxy` (через `proxy` работает Traefik). Контейнеры на `proxy` больше не резолвят `auth-postgres`/`auth-redis`.
- **Redis.** `requirepass` (48-hex, генерируется и хранится в env стека), TLS на `tls-port 6379`, публичный plaintext-порт выключен (`--port 0`), `--tls-auth-clients no` + AUTH. Healthcheck проверяет TLS+AUTH. Сертификат — от приватного CA.
- **Postgres.** TLS (`ssl=on`) с сертификатом приватного CA; TCP-доступ только `hostssl` (`hba_file=/certs/pg_hba.conf`), безшифровые подключения отклоняются. Клиент ходит с `SSL Mode=VerifyFull` + `Root Certificate`.
- **Рантайм БД.** Создана роль `avaluxauth_app` (`LOGIN`, `NOSUPERUSER`, `NOCREATEDB`, `NOCREATEROLE`, `NOBYPASSRLS`) — владелец БД и схемы `public`; `CONNECT` отозван у `PUBLIC`. Приложение подключается только этой ролью; суперпользователь остаётся для initdb/админских задач. Таблицы/последовательности переведены на `avaluxauth_app`, EF-миграции на старте применяются от неё.
- **Секреты в рантайме.** `auth` больше не использует `env_file: stack.env`: окружение задано явно, поэтому пароль суперпользователя (`DB__PASS`) и сырые `DB__APP_PASS`/`REDIS__PASSWORD` не попадают в контейнер приложения (они есть только в строках подключения).
- **TLS-материал.** Том `auth_auth_tls` (монтируется `:ro`): `ca.crt`, `ca-bundle.crt`, `postgres.crt/key` (0600, uid 70), `redis.crt/key` (0600, uid 999), `pg_hba.conf`; срок действия — до 2036-11-15. Приватный ключ CA — в отдельном томе `auth_auth_tls_ca`, в контейнеры не монтируется.
- **Код.** В `:master` добавлена проверка TLS Redis по приватному CA (`Security:RedisCaCertificatePath`) — см. [Program.cs:56-82](Api/AvaluxAuth.Api/Program.cs#L56-L82). На живом контуре этот код пока не запущен (см. ниже), там CA добавляется через `SSL_CERT_FILE` в бандл системных корней.

**Порядок деплоя (важно).** Образ `auth` закреплён по digest на сборку `c4c9617` — это состояние до H-02/H-04/H-05. Причина: в `:master` уже лежит H-05, которая требует PKCE и ломает десктоп-клиенты ReportChecker Studio/CLI/VS Code на `Avalux.Auth.UserClient` 1.3.2 (в `:master` пакет 1.4.0 ещё не опубликован). Поэтому H-07 выкачен на прежней ревизии, а TLS Redis там доверяется через `SSL_CERT_FILE=/certs/ca-bundle.crt`. Миграция `OAuthClientBinding` при временной проверке `:master` уже применилась к БД, поэтому при переходе на `:master` повторно не выполнится. После обновления клиентов и перевода стека на `:master`: вернуть тег `:master`, убрать `SSL_CERT_FILE` (CA начнёт проверяться явно), снять временные `DEFAULT ''`/`now()` с `RefreshTokens.ClientId/CreatedAt/ExpiresAt` и добить `ClientId` для токенов, выданных в переходный период (SQL — в `deploy/README.md`).

**Проверка (2026-11-19).**

- Из контейнера `dsh` (сеть `proxy`) `auth-postgres:5432` и `auth-redis:6379` — `EAI_AGAIN`; до фикса Redis отвечал `+PONG`, Postgres принимал TCP.
- Redis: plaintext → `Connection reset by peer`; `redis-cli --tls --cacert /certs/ca.crt ... ping` → `PONG`; без пароля → `NOAUTH`.
- Postgres: `pg_stat_ssl` для сессии приложения — `avaluxauth_app`, `ssl=true`; `sslmode=disable` → `no pg_hba.conf entry ... no encryption`.
- Сквозной сценарий: `/authorize` → 302, `state` записан в Redis, `/password/clientInfo` вернул 200 `{"name":"ReportChecker"}`.
- Рестарт `auth-postgres`/`auth-redis` не ломает конфигурацию: контейнеры поднимаются healthy, приложение переподключается.

**Остаточный риск.**

- TLS защищает от сниффинга в bridge-сети, но не от root на хосте; `auth` и хранилища по-прежнему на одной ВМ.
- Мониторинга/аудит-лога доступа к Redis и Postgres нет — в инфраструктуре нет стека мониторинга (L-10).
- Временный `DEFAULT` на `RefreshTokens.ClientId`/`CreatedAt`/`ExpiresAt` нужен только для работы старой ревизии; после деплоя `:master` его нужно снять.
- Клиентские сертификаты/ACL для Redis не используются: аутентификация — пароль + TLS; при желании можно перейти на ACL-пользователя.

---

## 3. Medium

### M-01. Анонимная запись в S3 без квот и очистки — ИСПРАВЛЕНО В КОДЕ

**Где:** [AvatarController.cs](Api/AvaluxAuth.Api/Controllers/AvatarController.cs) — `POST /api/v1/avatar` без `[Authorize]`, до 10 МБ на файл; `ClearFilesCreatedBefore` ([S3Repository.cs](Api/AvaluxAuth.S3/S3Repository.cs)) не вызывался ниоткуда.

**Влияние.** Без учётной записи можно было загружать изображения и получать публичные URL — бесплатный хостинг картинок. Объекты не удалялись никогда, storage и стоимость росли без границы. Декодирование чужих изображений на этом же эндпоинте — это ещё и поверхность H-03.

**Что сделано (2026-11-19, коммит `f6651e7`).**

- **Загрузка привязана к авторизации.** `POST /api/v1/avatar?state=…` выполняется только при живом `state` в Redis (`IStateRepository.GetStateAsync`); без `state` или с неизвестным — `401`. Это тот же state, в рамках которого AuthUI показывает форму входа/регистрации/профиля: UI теперь передаёт его вместе с файлом ([api.ts](AuthUI/src/lib/api.ts), [AvatarInput.svelte](AuthUI/src/lib/AvatarInput.svelte)). Админ-UI эндпоинт не использует (сгенерированный клиент `avatarPOST` мёртв).
- **Квота на state.** `IStateRepository.TryRegisterUploadAsync` инкрементит `AuthorizationState-{state}-avatar-uploads` (TTL 1 ч, как у самого state) и разрешает не больше `Config.MaxAvatarUploadsPerState = 5` попыток. Попытка считается до декодирования, поэтому битые файлы квоту не экономят; счётчик удаляется вместе со state (`DeleteStateAsync`/`TakeStateAsync`).
- **Rate limit по IP.** Политика `AvatarUpload` — 10 запросов / 10 мин на клиентский IP, `429` + `Retry-After` и JSON-тело (в стиле H-06). Ограничивает, сколько state один клиент успевает «сжечь».
- **Очистка сирот.** [AvatarCleanupService.cs](Api/AvaluxAuth.Api/AvatarCleanupService.cs) (BackgroundService): через минуту после старта и далее каждые `AvatarCleanup:Interval` (по умолчанию 24 ч) удаляет объекты старше `AvatarCleanup:MinAge` (по умолчанию 24 ч), на которые никто не ссылается. «Ссылается» = `Passwords.AvatarId` (включая soft-deleted строки) ∪ `Accounts.AvatarUrl`, содержащий `/api/v1/avatar/{id}` — [PasswordRepository.GetAvatarIdsAsync](Api/AvaluxAuth.DataAccess/Repositories/PasswordRepository.cs), [AccountRepository.GetAvatarUrlsAsync](Api/AvaluxAuth.DataAccess/Repositories/AccountRepository.cs). Настройки переопределяются через env `AvatarCleanup__Interval` / `AvatarCleanup__MinAge` (TimeSpan, например `12:00:00`); значения вне `(0, 30 дн]` игнорируются с предупреждением.
- **Листинг бакета.** `S3Repository.ClearUnreferencedFilesAsync` заменил неиспользуемый `ClearFilesCreatedBefore`: `ListObjectsV2` с `ContinuationToken` (прежний код игнорировал пагинацию и на бакете больше одной страницы чистил бы только первую 1000 объектов), id файла берётся из ключа `{id}/avatar.png` (поддержан и старый плоский `{id}.ext`). Удаление идемпотентно, поэтому сервис безопасно работает при нескольких репликах.
- **Побочно.** Неизображение или обрезанный файл теперь `400 Unsupported image` вместо 500; успешные загрузки логируются (id и IP, без state).

**Проверка (2026-11-19, локальный запуск исправленной сборки).** Postgres 17 и Redis 7 — отдельный dev-стек, S3 — локальная заглушка с PutObject/GetObject/DeleteObject и постраничным ListObjectsV2 (реальный MinIO/S3 в тестовом окружении недоступен; подпись заглушка не проверяет).

| Проверка | Результат |
|---|---|
| `POST /api/v1/avatar` без `state` / с неизвестным `state` | 401 / 401, объект не создан |
| 5 загрузок с одним state | 200 ×5 |
| 6-я загрузка с тем же state | 429 `Too many avatar uploads for this session.` |
| Счётчик `AuthorizationState-…-avatar-uploads` | 6 (попытки, а не успехи), TTL 3599 с |
| 11-й запрос с одного IP (политика `AvatarUpload`) | 429 + `Retry-After` + JSON `too_many_requests` |
| Загрузка текстового файла | 400 `Unsupported image` |
| Очистка: 8 объектов, 1 привязан через `Passwords.AvatarId` | удалено 7, оставлен 1 (листинг — 4 страницы по 2 объекта, continuation token работает) |
| Привязанный аватар после очистки | `GET /api/v1/avatar/{id}` → 200, валидный PNG |
| Сирота после очистки | `GET` → 500 (объекта нет; обработка скачивания не менялась) |
| Новая загрузка с новым state после очистки | 200 |
| `dotnet build AvaluxAuth.slnx`; `npm run check` + `npm run build` (AuthUI) | 0 ошибок |

**Деплой.** Код в `master` (`f6651e7`), на живой контур не выкачен — уедет вместе с H-02/H-04/H-05/H-06 при переводе стека на `master`. Контракт эндпоинта изменился: внешние вызовы `POST /api/v1/avatar` без `state` начнут получать 401 (в репозитории, кроме AuthUI, таких вызовов нет).

**Остаточный риск.**

- `state` выдаётся любому, кто начнёт `/authorize` с публичным `client_id`, поэтому квота на state — не аутентификация. Границу задают per-IP лимит и очистка. Сам `/authorize` не rate-limited (H-06 закрыл только вход и админку; для `/authorize` это отдельная задача): при ботнете число загрузок растёт, но сироты живут не дольше `MinAge` (24 ч), а хранимый файл всегда 200×200 PNG (~КБ).
- Per-IP лимит — per-process (реплика `auth` одна) и обходится сменой IP — та же оговорка, что в H-06.
- Очистка опирается на БД: аватар, загруженный перед регистрацией и не сохранённый в профиль, считается сиротой (это и есть цель), окно `MinAge` — сутки.
- Удаляются только объекты; `Accounts.AvatarUrl`/`Passwords.AvatarId`, указывающие на уже удалённый объект, не чинятся (скачивание отвечает 500 — это M-08/L-11).
- Декодерные лимиты ImageSharp (пиксели/TIFF-бомбы) не добавлены — остаток H-03.

### M-02. Будущие подписки выдаются сразу — ИСПРАВЛЕНО В КОДЕ

**Где:** проекции подписок [UserRepository.cs:171](Api/AvaluxAuth.DataAccess/Repositories/UserRepository.cs#L171) (`UserWithAccounts`) и [:194](Api/AvaluxAuth.DataAccess/Repositories/UserRepository.cs#L194) (`UserWithSubscriptions`); потребитель claim — [AuthorizationService.cs:150](Api/AvaluxAuth.Services/AuthorizationService.cs#L150).

**Причина.** Обе проекции фильтровали только `ExpiresAt > DateTime.UtcNow`, а `StartsAt` использовался лишь при выводе поля (`StartsAt = e.StartsAt ?? e.CreatedAt`). Поэтому подписка, оформленная с будущим `StartsAt` (предоплаченное продление, заранее согласованный апгрейд), сразу попадала в claim `Subscriptions` access-токена — то есть план выдавался до начала оплаченного периода. Тот же фильтр без `StartsAt` отдавал её и в админ-API (`GET …/users/{id}`, список пользователей).

**Влияние.** Пользователь получал entitlements будущего плана досрочно: «продление со следующего месяца» работало как немедленный апгрейд, а весь период до `StartsAt` не оплачен. Эксплуатация требует создать подписку с будущим `StartsAt` через админ-API `POST /api/v1/admin/apps/{applicationId}/users/{userId}/subscriptions` ([UsersController.cs:71](Api/AvaluxAuth.Api/Controllers/UsersController.cs#L71)); текущий админ-UI поле `StartsAt` не передаёт (`giveSubscription` отправляет только `expiresAt`, и сам метод нигде не вызывается), поэтому на практике это сценарий интеграций и ручных API-вызовов.

**Что сделано (2026-11-19, коммит `6dfe8c0`).**

- Обе проекции переведены на общий предикат [IsActiveAt](Api/AvaluxAuth.DataAccess/Repositories/UserRepository.cs#L210): `(StartsAt ?? CreatedAt) <= now && ExpiresAt > now` — интервал активности полуоткрытый `[StartsAt, ExpiresAt)`, граница `StartsAt == now` включена.
- `StartsAt = null` (строки до миграции `20260317070709_UserSubscription.StartsAt`) трактуется как `CreatedAt` — ровно то значение, которое проекция уже отдавала наружу как `StartsAt`, поэтому поведение для старых строк не изменилось.
- `now` вычисляется один раз на проекцию, чтобы все подписки одного ответа оценивались в один момент времени.
- Покрыты обе точки: claim `Subscriptions` (`GetUserWithSubscriptionsAsync` → `AuthorizationService.CreateAccessToken`, а значит и `/auth/token`, и `/auth/refresh`) и ответы админ-API (`GetUserWithAccountsAsync`, `GetUsersAsync`).

**Проверка (2026-11-19).** Временный harness вне репозитория: реальная EF-схема (миграции проекта) на Postgres 17 из dev-контура, отдельная БД `m02_check`, вызовы настоящего `UserRepository`. Один пользователь, 7 подписок разных состояний.

| Подписка | До фикса | После фикса |
|---|---|---|
| активная (`StartsAt` −1 дн, `ExpiresAt` +10 дн) | в claim | в claim |
| будущая (`StartsAt` +10 дн) | **в claim** | не в claim |
| `StartsAt` null, `CreatedAt` +5 дн | **в claim** | не в claim |
| `StartsAt` ровно сейчас (граница) | в claim | в claim (включительно) |
| legacy: `StartsAt` null, `CreatedAt` −10 дн | в claim, `StartsAt` = `CreatedAt` | так же |
| истёкшая (`ExpiresAt` −10 дн) / вывернутая (`StartsAt` > `ExpiresAt`) | нет | нет |
| claim `Subscriptions` | `legacy-future;future;boundary;active;legacy` (5) | `active;boundary;legacy` (3) |
| админ-проекция `GetUserWithAccountsAsync` / `GetUsersAsync` | 5 подписок | 3 |

`dotnet build AvaluxAuth.slnx` — 0 ошибок (2 прежних предупреждения CS8774 в `UserClient`, к изменению не относятся).

**Остаточный риск.**

- Claim — снимок на момент выпуска токена: подписка, начинающаяся внутри жизни access-токена (1 ч), появится в claim только в следующем access/refresh-токене. Это fail-closed — досрочного доступа не даёт, задержка ограничена часом.
- Админ-API больше не показывает ещё не начавшиеся подписки: в `GET …/users/{id}` и в списке их нет, пока не наступит `StartsAt`. В БД записи сохраняются (`SELECT * FROM "UserSubscriptions" WHERE "StartsAt" > now()`), а админ-UI подписки пользователя сейчас не рендерит — видимой регрессии в интерфейсе нет. Если понадобится показывать запланированные подписки администратору, нужен отдельный DTO/флаг активности, а не общий фильтр.
- `StartsAt >= ExpiresAt` при создании подписки не валидируется ([AddSubscriptionRequestSchema](Api/AvaluxAuth.Api/Schemas/AddSubscriptionRequestSchema.cs)): такая подписка просто никогда не активна — это вопрос поддержки/UX, а не безопасности.
- Отзыв доступа раньше `ExpiresAt` (сокращение срока подписки) по-прежнему ждёт истечения уже выданного access-токена (до 1 ч): M-03 отзывает сессии только при смене учётных данных (пароль, линковка), подписки к этому не относятся. Не M-02.

### M-03. Смена пароля не инвалидирует сессии; refresh-токены бессрочные — ИСПРАВЛЕНО В КОДЕ

**Где (на момент аудита):** [PasswordService.cs](Api/AvaluxAuth.Services/PasswordService.cs) меняет только хеш; [RefreshTokenEntity.cs](Api/AvaluxAuth.DataAccess/Entities/RefreshTokenEntity.cs) без `CreatedAt/ExpiresAt/ClientId`; `JwtBearerEvents.OnTokenValidated` ([Program.cs](Api/AvaluxAuth.Api/Program.cs)) выходил сразу, если в токене нет `TokenId`, то есть проверял только service-account токены (H-04).

**Причина.** Смена пароля меняла только BCrypt-хеш. Все ранее выданные refresh-токены оставались рабочими (и, до H-05, бессрочными), access-токены — до 1 часа. При компрометации аккаунта смена пароля не выкидывала атакующего; то же при линковке нового способа входа, через который потом можно войти, зная только новый пароль/провайдера.

**Что сделано (2026-11-19, коммит `5090e0b`).**

- **Первая половина закрыта в H-05 (2026-11-18):** `RefreshTokens.ClientId/CreatedAt/ExpiresAt`, абсолютный срок 30 дней (`Security:RefreshTokenLifetimeDays`), ротация срок не продлевает, токен привязан к клиенту, просроченный удаляется. Оставалось главное: смена пароля и линковка ничего не отзывали, а пользовательские access-токены не отзывались вообще (это же отмечено в остаточном риске H-04).
- **Поколение сессии.** Новое поле `Users.SessionVersion` (миграция `20261119095535_UserSessionVersion`: `integer NOT NULL DEFAULT 0`). Access-токен несёт claim `SessionVersion` ([AuthorizationService.cs](Api/AvaluxAuth.Services/AuthorizationService.cs)). В `OnTokenValidated` для любого токена с claim `UserId` текущее поколение читается из БД и несовпадение отвергает запрос (401). Fail closed: удалённый или отсутствующий пользователь (`GetSessionVersionAsync` → `null`) и ошибка БД тоже дают 401. Токены, выданные до миграции, claim не содержат и читаются как 0 — ровно значение по умолчанию колонки, поэтому деплой никого не разлогинивает.
- **Revoke-all одной транзакцией.** `IUserRepository.RevokeSessionsAsync`: `SessionVersion += 1` и `DELETE FROM "RefreshTokens" WHERE "UserId" = …` в одной транзакции (иначе падение между операциями оставило бы refresh-токен, способный выпустить токен с новым поколением). Вызывается из [PasswordService.ChangePasswordAsync](Api/AvaluxAuth.Services/PasswordService.cs) после успешной смены пароля и при линковке нового способа входа: `PasswordService.GetOrCreateAccountAsync` (link_code + пароль) и [OauthService.ProcessCodeAsync](Api/AvaluxAuth.Services/OauthService.cs) (link_code + внешний провайдер). Обычный вход и обычное обновление профиля поколение не меняют.
- **Коды авторизации привязаны к поколению.** `AuthCode.SessionVersion` заполняется при выпуске кода. Код, выданный до смены пароля/линковки, при обмене получает `invalid_grant` — иначе атакующий, уже получивший код (но не успевший его обменять), после отзыва сессий выпустил бы себе токен с *новым* поколением. Коды, лежавшие в Redis до деплоя, десериализуются с `SessionVersion = 0` и остаются рабочими, пока поколение 0.
- **Порядок в потоках.** В профиле и в линковке код выпускается уже после отзыва (`CreateResponseSchemaAsync` читает поколение после `ChangePasswordAsync`/`GetOrCreateAccountAsync`), поэтому сценарий, который сам сменил пароль или привязал аккаунт, продолжает работать и сразу получает свежие токены, а остальные клиенты и устройства выкидывает.
- **Логи.** `All sessions revoked: userId=…, reason=password-changed|account-linked`; отказ — `Rejected access token with a stale session version: userId=…, tokenVersion=…`. Логины/секреты в лог не попадают.
- **Стоимость.** Один запрос к БД по первичному ключу на каждый запрос с пользовательским bearer-токеном (эндпоинты `/auth/userinfo`, `/auth/linkCode`, `/auth/{providerKey}/accessToken`). Кэша нет: отзыв должен действовать сразу. Путь service-account токенов не изменился (H-04) — у них по-прежнему Redis+БД по `TokenId`.

**Проверка (2026-11-19).** Одноразовый стенд `avaluxauth-dev` (Postgres 17 + Redis 7, удалён после прогона), API из этой ветки на `127.0.0.1:5099`, приложение с публичным `client_id` и провайдером «логин и пароль», PKCE S256, миграции применены на старте. **30/30 проверок** (в таблице часть строк объединяет по две проверки).

| Проверка | Результат |
|---|---|
| `/auth/userinfo` с токеном после регистрации; claim `SessionVersion` | 200; `0` |
| Смена пароля через `/password/profile` (link_code → profile state) | 200 + новый код |
| Старый access-токен после смены пароля | **401** |
| Старый refresh-токен после смены пароля (`grant_type=refresh_token`) | **400 `invalid_grant`** |
| Токен, выпущенный по коду из ответа на смену пароля | 200, `SessionVersion=1` |
| Ротация нового refresh-токена | 200 |
| Старый пароль при входе / новый пароль | 401 / 200 (токен рабочий) |
| Обычный вход не меняет поколение | `SessionVersion` остаётся `1` |
| БД после смены пароля: `Users.SessionVersion`, число refresh-токенов | `1`; остались только выданные после смены |
| Линковка: пользователь без пароля (строка `Users` + refresh-токен в БД), `/auth/linkCode` → `/authorize?link_code=…` → signup | `/login?state=…`, 200 + код |
| Access-токен, выданный до линковки / refresh-токен до линковки | **401** / **400 `invalid_grant`** |
| Токен из потока линковки | 200, `SessionVersion=1` |
| Код, выпущенный до смены пароля, но обменянный после | **400 `invalid_grant`** |
| Код, выпущенный после смены пароля | 200 |
| Удалённый пользователь (soft-delete) со своим валидным токеном | **401** (`GetSessionVersionAsync` → `null`) |
| Регрессия H-04: service-account токен → `/service/users`; после `DELETE …/tokens/{id}` | 200; затем 401 |
| `dotnet build AvaluxAuth.slnx`, `dotnet build -c Release` (API) | 0 ошибок (единственное предупреждение — прежний CS9113 в `ApplicationsController`, файл не менялся) |

**Деплой.** Код в `master` (`5090e0b`), миграция применяется автоматически на старте API. На живом контуре пока закреплена сборка `c4c9617`; M-03 уедет вместе с H-02/H-04/H-05/H-06 при переводе стека на `:master`. Совместимость в обе стороны: `SessionVersion` имеет `DEFAULT 0`, старая ревизия колонку не упоминает и вставляет пользователей без неё; при откате на старую ревизию отзыв просто перестаёт работать (колонка остаётся). Значение по умолчанию не разлогинивает никого при деплое.

**Остаточный риск.**

- **Сторонняя валидация токенов.** Сервисы, которые проверяют access-токен локально по JWKS (ReportChecker API и др.), о поколении не знают: отозванный access-токен там живёт до `exp` (≤1 ч). Refresh-токен там бесполезен — обмен идёт только через этот API. Полное закрытие — introspection endpoint или централизованная проверка, вне объёма M-03.
- **In-flight запросы.** Проверка выполняется в `OnTokenValidated`; запрос, уже прошедший аутентификацию в момент отзыва, доработает. Окно — длительность одного запроса.
- **Fail closed по БД.** При недоступности Postgres пользовательские эндпоинты отвечают 401, а не работают по подписи токена. Это осознанный выбор: иначе отзыв можно обойти отказом БД.
- **Разлогин всех клиентов.** Смена пароля и линковка выкидывают все сессии, включая текущую (UI получает новый код в том же ответе, сторонние приложения — повторный вход). Для линковки это значит, что пользователь, привязавший новый способ входа, перелогинивается во всех приложениях.
- **Нет управления сессиями.** Endpoint'а «выйти на всех устройствах» и списка активных сессий нет — M-03 этого не требовал, но как UX/безопасность стоит добавить.
- **Подписки не относятся к сессиям.** Сокращение подписки по-прежнему ждёт истечения access-токена (≤1 ч), см. остаточный риск M-02.
- Смена пароля при недоступной БД: хеш уже обновлён, а отзыв упал — клиент получит 500. Отзыв идёт в той же БД, что и смена хеша, поэтому сценарий практически означает полный отказ БД.

### M-04. Ротация signing key не инвалидирует старые ключи

[SigningKeyRepository.cs](Api/AvaluxAuth.DataAccess/Repositories/SigningKeyRepository.cs): `AddAsync` деактивирует старый ключ, но `GetAllAsync`/JWKS отдают его вечно, `ExpiresAt` не проверяется. Утёкший старый приватный ключ остаётся валидным. Публиковать только ключи в окне overlap и удалять/истекать старые.

### M-05. Секреты и PII в логах — ИСПРАВЛЕНО В КОДЕ

**Где:** [GoogleAuthProvider.cs](Api/AvaluxAuth.Providers/GoogleAuthProvider.cs) — две `Console.WriteLine` выводили redirect URL и полный authorization URL вместе с одноразовым `state`; [MicrosoftAuthProvider.cs](Api/AvaluxAuth.Providers/MicrosoftAuthProvider.cs) — в stdout писалось фото пользователя в base64 (`data:image/jpeg;base64,…`); [OauthService.cs](Api/AvaluxAuth.Services/OauthService.cs) — две `Console.WriteLine` с признаками link-кода и уже привязанного аккаунта; [ImageService.cs](Api/AvaluxAuth.Services/ImageService.cs) — список embedded-ресурсов на каждый рендер аватара.

**Влияние.** `state` — одноразовый секрет потока авторизации (вместе с PKCE и сжиганием state из H-06 он защищает от CSRF и подмены кода), и полный auth URL в логе обесценивает эту защиту для того, кто читает логи. Base64-фото — это PII в открытом виде и раздувание лога на каждый вход. Признаки link-состояния — утечка бизнес-логики. Кроме того, `Console.WriteLine` обходил настроенные уровни логирования и структурирование: такие строки нельзя ни отфильтровать, ни отредактировать.

**Что сделано (2026-11-19, коммит `90a23db`).**

- **`Console` в API больше нет.** Все четыре площадки переведены на `ILogger`; `grep -rn "Console\." Api --include=*.cs` пуст. `Console` в `AvaluxAuth.TestCli` — это штатный вывод CLI для оператора (его же данные), к логам сервера отношения не имеет и остаётся в рамках C-03.
- **Google.** В `GoogleAuthProvider` внедрён `ILogger<GoogleAuthProvider>`; вместо URL пишется `LogDebug("Google authorization URL built: redirectUri={RedirectUri}")` — только redirect URI, то есть публичная конфигурация приложения. `state`, `client_id` и `client_secret` в лог не попадают, `UrlBuilder.ToString()` вычисляется один раз.
- **Microsoft.** Удалён `Console.WriteLine(await GetUserPhotoAsync(...))`. Побочно исчез второй запрос `GET /me/photo` в Graph на каждый вход: фото и так запрашивалось для `AvatarUrl`.
- **OauthService.** Две `Console.WriteLine(bool)` заменены одним `LogDebug` со структурными булевыми полями `LinkCodePresent`/`AccountAlreadyLinked`; код линковки, `userId` и данные аккаунта не логируются.
- **ImageService.** Внедрён `ILogger<ImageService>`; список embedded-ресурсов пишется `LogError` только на ветке «шрифт не найден» (диагностика сохранена, секретов и PII там нет), а не на каждый рендер аватара.
- **Санитайзинг недоверенного текста.** Новый [LogSanitizer.cs](Api/AvaluxAuth.Utils/LogSanitizer.cs): управляющие символы (`\r`, `\n`, `\t`, `NUL`, `DEL`) заменяются пробелом, значение обрезается (512 символов по умолчанию, 64 для `error`), пустое значение превращается в `[empty]`/`[blank]`. Применён в [GitLabAuthProvider.cs](Api/AvaluxAuth.Providers/GitLabAuthProvider.cs) и [MicrosoftAuthProvider.cs](Api/AvaluxAuth.Providers/MicrosoftAuthProvider.cs) там, где в текст исключения попадают `error`/`error_description` из query и тело ответа провайдера. Это закрывает хвост H-02 («сырой `error_description` в логах») и исключает log forging (подделку строки лога переводом строки) и log flooding (неограниченный размер записи).
- **Проверено, что стандартное request-логирование не светит коды и state.** `Microsoft.AspNetCore.Hosting.Diagnostics` пишет query string на уровне `Information`, но в [appsettings.json](Api/AvaluxAuth.Api/appsettings.json) категория `Microsoft.AspNetCore` поднята до `Warning`, а стек `auth` не переопределяет `Logging__*`. Значит, GET-callback с `?code=…&state=…` в лог не попадает. Это условие зафиксировано: если кто-то включит `Microsoft.AspNetCore=Information`, коды авторизации и state окажутся в логах.

**Проверка после фикса** (локальный прогон провайдеров с тестовым `ILogger` и пустым `IHttpClientFactory`, без внешних вызовов):

| Проверка | Результат |
|---|---|
| `GoogleAuthProvider.GetAuthUrl` со `state=STATE-SECRET-…`, тестовым `client_id` и secret | URL содержит state; в логе `log_leaks_state=false`, `log_leaks_client_id=false`, `log_leaks_secret=false`, единственная строка — про `redirectUri` |
| `MicrosoftAuthProvider.GetTokenAsync` с `error_description="invalid_request\r\n… Forged admin sign-in succeeded\t"` | исключение из одной строки, `\r`/`\n` отсутствуют; payload остаётся текстом с пробелами |
| То же с `error_description` длиной 5000 символов | сообщение 551 символ (512 + маркер `…`) |
| `GitLabAuthProvider.GetTokenAsync` с `error=invalid_scope`, `error_description` с `\r\n` | в тексте исключения нет `\r`/`\n` |
| `LogSanitizer.Sanitize` на `null`, `""`, `"   "` | `[empty]`, `[empty]`, `[blank]` |
| `grep -rn "Console\." Api --include=*.cs` | совпадений нет |
| `dotnet build AvaluxAuth.slnx`, `dotnet build -c Release` (API) | 0 ошибок (прежние предупреждения CS8774/CS9113 в файлах, которые не менялись) |

**Деплой.** Изменение в `master` (`90a23db`). Для API это чисто логирование: контракты, коды ответов и формат данных не менялись, миграций нет. Уедет на живой контур вместе с H-02/H-04/H-05/H-06/M-01/M-02/M-03 при переводе стека на `:master`; откат безопасен в обе стороны.

**Остаточный риск.**

- **Аудит-след неудачных входов.** `PasswordController` и `AdminController` по-прежнему пишут IP и хеш логина (H-06). Это осознанный security-лог, логин хешируется, но IP остаётся персональными данными; срок хранения ограничен ротацией `json-file` 1m×1, централизованного хранилища и политики ретенции нет (L-10).
- **Стек и сообщения исключений.** `AuthController.ErrorHtml` пишет исключение целиком, включая stack trace: сообщения провайдеров санитайзятся на источнике, но любое новое место, где в текст исключения попадёт недоверенный ввод, нужно проводить через `LogSanitizer`. Это договорённость, а не принудительный механизм.
- **PII вне логов.** Base64-фото Microsoft по-прежнему возвращается провайдером как `data:`-URL и попадает в `UserInfo.AvatarUrl`. M-05 убрал его только из лога; хранение PII — отдельный вопрос.
- **Сторонние сборщики логов.** Если контейнерный лог начнут отправлять во внешнюю систему, политику ретенции и редактирование нужно настраивать там же; в приложении редактирование сейчас точечное (санитайзер + отказ от логирования секретов).

### M-06. Нет security-заголовков

Ни Kestrel, ни [nginx.conf](UI/nginx.conf) не отдают CSP/HSTS/X-Frame-Options/X-Content-Type-Options/Referrer-Policy; `server: Kestrel`. Добавить на Traefik/приложение/nginx; `frame-ancestors 'none'`, `strict-transport-security`, `referrer-policy: no-referrer` на auth-страницах.

### M-07. DataProtection keys: 777 и без шифрования at rest

[Dockerfile:38-39](Dockerfile#L38-L39), на живом контейнере `drwxrwxrwx`. Ключи шифруют приватные signing keys и provider-токены в БД. Сделать `700`, отдельный volume, `SetApplicationName`, рассмотреть `ProtectKeysWithCertificate`/KMS.

### M-08. Вербозные 500 и необработанные исключения

[RedisStateRepository.cs:18-23](Api/AvaluxAuth.Services/RedisStateRepository.cs#L18-L23) — `JsonSerializer.Deserialize` от пустого значения → 500 (подтверждено; **исправлено в рамках H-06**: отсутствующий ключ → `null`/401); [PasswordController.cs:42,68](Api/AvaluxAuth.Api/Controllers/PasswordController.cs#L42) возвращает `e.Message`. Возвращать 400/401 и generic-сообщения, детали — в лог. `avatar?color=999` → 500 (валидировать `colorIndex`).

### M-09. CORS по умолчанию `AllowAnyOrigin`

[Program.cs:103-116](Api/AvaluxAuth.Api/Program.cs#L103-L116) применяется к `/auth/token`, `/avatar` и др. Ограничить известными origin; `localhost:4200` с `AllowCredentials` — только для dev.

### M-10. Небезопасные redirect URI в проде

В БД: `https://report-checker.vercel.app/auth/callback`, `http://localhost:4200/auth/callback`, `http://localhost:14872`. Убрать лишнее, оставить только прод-HTTPS; для dev — отдельный клиент. В сочетании с H-05 это путь к захвату аккаунтов.

### M-11. Admin login: fail-open на пустом пароле и неконстантное сравнение

[AdminController.cs:23-24](Api/AvaluxAuth.Api/Controllers/AdminController.cs#L23-L24): если `Admin:Password` не задан/пуст, сравнение с `null`/`""` проходит. Проверять конфиг на старте (non-empty), сравнивать `CryptographicOperations.FixedTimeEquals`, хранить хеш. На момент аудита переменная задана, но паттерн опасный.

**Частично исправлено вместе с H-06 (2026-11-19):** вход fail-closed при незаданном/пустом конфиге, сравнение — SHA-256 + `FixedTimeEquals`. Остаются проверка конфига на старте и хранение хеша пароля вместо открытого значения.

### M-12. Нет unique index на `Passwords.Login`

Миграция [20260320105223_Passwords.cs](Api/AvaluxAuth.DataAccess/Migrations/20260320105223_Passwords.cs); signup делает `CheckUserExistsAsync` → `CreateAsync` (гонка). Добавить unique index + обработку конфликта, нормализацию логина.

---

## 4. Low / hardening

- **L-01. JWT.** [Program.cs:75](Api/AvaluxAuth.Api/Program.cs#L75) `RequireHttpsMetadata=false` (metadata — loopback HTTP, приемлемо) и [Program.cs:81](Api/AvaluxAuth.Api/Program.cs#L81) `ValidateAudience=false`. Валидировать audience хотя бы для service-account (`AvaluxAuthServiceAccount`).
- **L-02. Host-заголовки.** [Extensions.cs](Api/AvaluxAuth.Api/Extensions.cs): `GetBaseUrl` берёт `X-Forwarded-Host`/`X-Forwarded-Proto`. В текущем деплое Traefik перезаписывает заголовок (проверено), но при прямом доступе к Kestrel возможна подмена URL в openid-configuration. `AllowedHosts: "*"`.
- **L-03. TokenRepository.** [TokenRepository.cs:13](Api/AvaluxAuth.DataAccess/Repositories/TokenRepository.cs#L13) возвращал только удалённые токены (`DeletedAt != null`) — латентный баг. **Исправлено вместе с H-04:** метод отдаёт любую запись, а активность проверяют вызывающие.
- **L-04. Пагинация.** [UserRepository.cs:96](Api/AvaluxAuth.DataAccess/Repositories/UserRepository.cs#L96): `Take(limit).Skip(page*limit)` — неверный порядок; `limit` без верхней границы; отрицательный `page` → 500. **Исправлено в коде 2026-11-19** — см. «L-04. Пагинация — ИСПРАВЛЕНО В КОДЕ» ниже.
- **L-05. Google refresh.** [GoogleAuthProvider.cs:75](Api/AvaluxAuth.Providers/GoogleAuthProvider.cs#L75) отправлял `grant_type=authorization_code` вместо `refresh_token` — refresh Google не работал. **Исправлено в коде 2026-11-19** — см. ниже.
- **L-06. ApiClient.** `GetAccessTokenAsync` игнорирует `provider` — запрос без `providerKey` вернёт 400.
- **L-07. DB-суперпользователь.** ~~Приложение работает под `POSTGRES_USER`.~~ **Исправлено вместе с H-07:** рантайм работает под `avaluxauth_app`; суперпользователь остаётся только для initdb/админских задач.
- **L-08. CI.** `.github/workflows/main.yml` даёт `contents: write`, образы/actions не запинены по digest/SHA, сборка на каждый push; нет Trivy/SBOM/подписи.
- **L-09. `keys/`.** Не в `.gitignore`/`.dockerignore`. Локальный запуск создаёт key ring; добавить `**/keys/`. **Исправлено 2026-11-19** (`**/keys/` в `.gitignore`, `**/keys` в `.dockerignore`) — см. ниже.
- **L-10. Наблюдаемость.** Нет аудит-лога админских действий и централизованного хранения логов; у `auth` нет healthcheck/resource limits. Неудачные входы с 2026-11-19 логируются в stdout (H-06), но только с ротацией 1m.
- **L-11. Avatar GET.** `text` до ~8 КБ и рендер шрифтом 100px — CPU/память DoS; `colorIndex` вне диапазона → 500.

### L-04. Пагинация — ИСПРАВЛЕНО В КОДЕ

**Где:** [UserRepository.cs:70](Api/AvaluxAuth.DataAccess/Repositories/UserRepository.cs#L70) `GetUsersAsync(page, limit)` и [UserRepository.cs:96](Api/AvaluxAuth.DataAccess/Repositories/UserRepository.cs#L96) `SearchUsersAsync`; точка входа — [UsersController.cs:22-23](Api/AvaluxAuth.Api/Controllers/UsersController.cs#L22-L23) (админка) и [ServiceAccountController.cs:28-29](Api/AvaluxAuth.Api/Controllers/ServiceAccountController.cs#L28-L29) (service-account API).

**Влияние.** Пагинация была сломана сразу в пяти местах:

1. **Перевёрнутый порядок.** В `SearchUsersAsync` стояло `Take(limit).Skip(page * limit)`: сначала брались `limit` строк, затем те же `limit` пропускались. Начиная со страницы 1 выборка всегда пуста — service-account поиск отдавал только первую страницу, хотя `total` считался по всей таблице. В `GetUsersAsync` порядок был верный, но страницы всё равно «плавали» из-за отсутствия `ORDER BY` (см. п. 5).
2. **Страница считалась по аккаунтам, а не по пользователям.** `SearchUsersAsync` пагинировал `Accounts`, и только потом группировал по `UserId`. У пользователя с несколькими аккаунтами они могли попасть на разные страницы, а число пользователей на странице было непредсказуемым (0…limit), поэтому листать выдачу было нельзя.
3. **`limit` без верхней границы.** `?limit=1000000` заставлял материализовать всех пользователей приложения вместе с аккаунтами и подписками — CPU/память DoS на админском и service-account API.
4. **Отрицательный `page` → 500.** `Skip(-N)` бросает `ArgumentOutOfRangeException`, исключение не обрабатывалось.
5. **Не было `ORDER BY`.** Даже с правильным `Skip/Take` без стабильного порядка страницы «плавают»: одна запись может попасть на две страницы или ни на одну. `page` к тому же игнорировался, когда `limit` не передан.

**Что сделано (2026-11-19, коммит `30c2b78`).**

- **Новый [Pagination.cs](Api/AvaluxAuth.Models/Pagination.cs)** — общие границы для репозиториев и контроллеров: `MaxLimit = 100`, `NormalizePage` (clamp `>= 0`), `NormalizeLimit` (clamp `1..100`; отсутствующий `limit` — это `MaxLimit`, а не «всё»), `Skip` (произведение `page * limit` в 64 битах с насыщением: `int.MaxValue * 100` больше не превращается в отрицательный OFFSET).
- **`GetUsersAsync(page, limit)`:** `OrderBy(CreatedAt).ThenBy(Id)` → `Skip(...)` → `Take(...)`; размер страницы нормализуется. Порядок `CreatedAt` сохраняет привычную админке «порядок добавления».
- **`SearchUsersAsync`:** пагинация по пользователям. Сначала одна страница `Distinct` пар `(UserId, User.CreatedAt)` с `ORDER BY`, затем одним запросом (`WHERE UserId = ANY(...)`) подгружаются аккаунты только этих пользователей; группировка выполняется в памяти с сохранением порядка страницы. Все аккаунты одного пользователя теперь всегда на одной странице.
- **Границы в контроллерах:** `[Range(0, int.MaxValue)] page` и `[Range(1, Pagination.MaxLimit)] limit`. С `[ApiController]` невалидные значения дают 400 `ValidationProblemDetails` вместо 500 или неограниченной выдачи; репозиторий применяет те же границы повторно (defense in depth для вызовов, идущих мимо контроллера).
- **Контракт:** `limit` — от 1 до 100; `page` — `>= 0`; отсутствующий `limit` в service-account API означает максимум 100 пользователей на страницу (раньше — все). Встроенный `AvaluxAuth.ApiClient` по умолчанию передаёт `limit=100`, админка — `20`, поэтому штатные клиенты изменения не заметят.

**Проверка после фикса.** Стенд: PostgreSQL 17 из dev-стека `reportchecker-dev`, все EF-миграции, 12 пользователей (у первого — 3 аккаунта, всего 14 аккаунтов), локальный запуск API на `127.0.0.1:5099`; админский вход по cookie, service-account токен создан через админский API. База после прогона удалена.

| Проверка | Результат |
|---|---|
| `GET /api/v1/admin/apps/{id}/users?page=0&limit=5` | 200, 5 пользователей, `total=12` |
| `...?page=1&limit=5` | 200, 5 пользователей — продолжение списка без пересечений и пропусков |
| `...?page=2&limit=5` | 200, 2 пользователя |
| `...?page=-1&limit=5` | 400 (`page`), не 500 |
| `...?limit=1000`, `...?limit=0` | 400 (`limit`) |
| `...` без `limit` | 200, 12 пользователей (админский путь без пагинации сохранён) |
| `GET /api/v1/service/users?page=0&limit=3` | 200, 3 пользователя, 5 аккаунтов (у первого — все 3) |
| `...?page=1&limit=3` / `page=2&limit=4` / `page=3&limit=3` | 200, страницы 4…6 / 9…12 / 10…12 — сдвиг ровно на `page*limit`; в старом коде `Take→Skip` страницы 1+ были бы пустыми |
| `...?page=-1&limit=3`, `...?limit=1000` | 400 (`page` / `limit`) |
| `...?email=u01` | 200, 1 пользователь с 3 аккаунтами: фильтр и группировка не разрезают пользователя |
| `...?limit=100` (граница) | 200, 12 пользователей |
| `...` без `limit` | 200, 12 пользователей — выдача ограничена `MaxLimit` |
| `GetUsersAsync(page: int.MaxValue, limit: 100)` (репозиторий) | пустая страница, без переполнения и без исключения |
| `dotnet build AvaluxAuth.slnx -c Release` | 0 ошибок (прежние предупреждения CS8774/CS9113) |

**Деплой.** Только код API, миграций нет. Изменение контракта описано выше; для админки и `ApiClient` оно прозрачно. Уедет на живой контур вместе с H-02/H-04/H-05/H-06/M-01/M-02/M-03/M-05 при переводе стека `auth` на `:master`.

**Остаточный риск.**

- **OFFSET-пагинация.** Глубокие страницы по-прежнему стоят `OFFSET N` (линейный скан); для больших таблиц нужен keyset-подход, но это уже изменение дизайна API.
- **Поиск не фильтрует `Users.DeletedAt`.** `SearchUsersAsync` отбирает `Accounts.DeletedAt == null`, но не `User.DeletedAt == null` (в отличие от `GetUsersAsync`), поэтому удалённые пользователи находятся поиском. Это не входило в L-04 и поведение не менялось; при следующем проходе стоит выровнять и это.
- **Админский путь без `limit`.** `GetUsers` с `limit == null` отдаёт всех пользователей приложения — путь остаётся неограниченным, но доступен только админу за VPN и сохранён для обратной совместимости.

### L-05. Google refresh — ИСПРАВЛЕНО В КОДЕ

**Где:** [GoogleAuthProvider.cs:75](Api/AvaluxAuth.Providers/GoogleAuthProvider.cs#L75) `RefreshTokenAsync` и [GoogleAuthProvider.cs:21](Api/AvaluxAuth.Providers/GoogleAuthProvider.cs#L21) `GetAuthUrl`.

**Влияние.** Обновление провайдерского Google-токена не работало, причём по трём независимым причинам:

1. **Неверный `grant_type`.** В refresh-запрос уходил `grant_type=authorization_code` вместе с `refresh_token`, но без `code`. Google отвечает на такое `400 invalid_request`, `EnsureSuccessStatusCode()` бросает исключение, и `UserService.GetAccessTokenAsync` для истёкшего Google-токена падал (500) вместо обновления.
2. **Не запрашивался offline-доступ.** Consent flow не отправлял `access_type=offline`, а Google выдаёт refresh-токен только для offline-гранта. То есть даже с исправленным `grant_type` обновлять было бы нечего: в БД не сохранялся refresh-токен.
3. **Затирание refresh-токена.** Google не возвращает `refresh_token` в ответе на refresh, а `UserService` сохраняет полученный объект целиком ([UserService.cs:52](Api/AvaluxAuth.Services/UserService.cs#L52), [UserService.cs:86](Api/AvaluxAuth.Services/UserService.cs#L86)). То есть как только `grant_type` был бы исправлен, первое же успешное обновление записало бы `RefreshToken = data.RefreshToken` (null) и стёрло сохранённый токен — следующее обновление стало бы невозможным.

**Что сделано (2026-11-19, коммит `1425071`).**

- `grant_type` в `RefreshTokenAsync` — `refresh_token`; `refresh_token`, `client_id` и `client_secret` передаются как раньше.
- В `GetAuthUrl` добавлен `access_type=offline`, но только когда `ProviderParameters.SaveTokens` включён, то есть приложение действительно собирается хранить токены. Для остальных конфигураций consent-экран не меняется.
- `RefreshToken = string.IsNullOrEmpty(data.RefreshToken) ? refreshToken : data.RefreshToken` — при отсутствии нового токена в ответе сохраняется использованный, при ротации (если Google её вернёт) берётся новый.
- Логирование не менялось: по-прежнему пишется только redirect URI (M-05), ни токены, ни `client_secret` в лог не попадают.

**Проверка после фикса** (провайдер с подменённым `HttpMessageHandler`, реальных запросов к Google нет):

| Проверка | Результат |
|---|---|
| Тело refresh-запроса | `client_id=…&client_secret=…&grant_type=refresh_token&refresh_token=old-refresh`; строки `grant_type=authorization_code` нет |
| Ответ `{"access_token":"new-access","expires_in":3600}` (без refresh-токена) | `AccessToken=new-access`, `RefreshToken=old-refresh` — сохранённый токен не потерян |
| Ответ с `"refresh_token":"rotated-refresh"` | `RefreshToken=rotated-refresh` — ротация учитывается |
| `GetAuthUrl` при `SaveTokens=true` | URL содержит `access_type=offline`, `state` и `response_type=code` |
| `GetAuthUrl` при `SaveTokens=false` | `access_type` отсутствует |
| `dotnet build AvaluxAuth.slnx -c Release` | 0 ошибок |

**Деплой.** Изменение только в провайдере `google`, миграций нет. Уже сохранённые Google-аккаунты без refresh-токена начнут получать его только после повторной авторизации через Google; до этого `GetAccessTokenAsync` для них возвращает `null`, как и раньше (то есть хуже не становится). Уедет на живой контур вместе с остальными исправлениями при переводе стека `auth` на `:master`.

**Остаточный риск.**

- **`prompt=consent` не запрашивается.** Если пользователь повторно проходит consent, а Google не выдаёт новый refresh-токен (например, доступ был отозван), токен появится только после явного пересогласия; для этого потребуется либо `prompt=consent`, либо повторный отзыв/выдача доступа на стороне Google.
- **Ошибки Google не разбираются.** Ответ с ошибкой обрабатывается через `EnsureSuccessStatusCode()`; текст ошибки в исключение не попадает, поэтому диагностика в логе ограничена статусом. Для Google это приемлемо (тело не содержит секретов), но при следующем проходе стоит добавить санитайзинг `error`/`error_description`, как в Microsoft/GitLab (M-05).
- **`access_type=offline` виден пользователю.** Consent-экран Google показывает «offline access»; это ожидаемая плата за сохранение токенов и включается только при `SaveTokens`.

### L-09. `keys/` в ignore-файлах — ИСПРАВЛЕНО

**Где:** [.gitignore](.gitignore), [.dockerignore](.dockerignore). Каталог создаётся при локальном запуске ([Program.cs:43](Api/AvaluxAuth.Api/Program.cs#L43), `PersistKeysToFileSystem(... ?? "./keys")`) и в образе ([Dockerfile:38](Dockerfile#L38)).

**Влияние.** Первый же локальный запуск API создаёт DataProtection key ring — например, `Api/AvaluxAuth.Api/keys/key-<guid>.xml`. Этими ключами зашифрованы приватные signing keys и провайдерские токены в БД, поэтому утечка каталога равносильна утечке ключей подписи и refresh-токенов провайдеров. Без правил игнорирования:

- `git add -A` / `git add .` закоммитил бы key ring в репозиторий (а он публичный);
- `docker build` отправил бы его в build context, а `COPY . .` в [Dockerfile:27](Dockerfile#L27) — внутрь образа, откуда ключи уехали бы в registry.

**Что сделано (2026-11-19, коммит `aa34d17`).**

- `.gitignore`: `**/keys/` — каталог `keys` любого уровня вложенности (`Api/AvaluxAuth.Api/keys`, корневой `keys/`), рядом с `.env`.
- `.dockerignore`: `**/keys` — в общем списке служебных каталогов (`**/bin`, `**/obj`, `**/node_modules`).
- Ключи живого контура и так лежат в отдельном volume `auth_keys` (`deploy/auth-stack.yml`), каталог в образе создаётся пустым; ignore-правила лишь закрывают побочные пути.

**Проверка после фикса.**

| Проверка | Результат |
|---|---|
| Локальный запуск API (проверка L-04) | создан `Api/AvaluxAuth.Api/keys/key-<guid>.xml` |
| `git status --porcelain` после запуска | каталог не появляется, видны только изменения задачи |
| `git check-ignore -v Api/AvaluxAuth.Api/keys/key-*.xml` | `.gitignore:11:**/keys/` |
| `git check-ignore -v keys/xml/key.xml` | `.gitignore:11:**/keys/` |
| `git ls-files` с фильтром `keys/` | пусто — key ring в истории отсутствует |
| `.dockerignore` | `**/keys` в списке; проверено чтением файла (docker CLI в среде аудита недоступен, сборка образа не выполнялась) |

**Деплой.** Только ignore-файлы; на работу приложения и существующие образы не влияет.

**Остаточный риск.** Права `777` на `/app/keys` и отсутствие шифрования ключей at rest остаются открытыми — это M-07, L-09 закрывает только утечку через git и build context.

---

## 5. Клиентские библиотеки (публикуются в NuGet)

### C-01. UserClient: command injection на Windows и неэкранированный provider

[AvaluxAuth.UserClient/AuthClient.cs:33-36](AvaluxAuth.UserClient/AuthClient.cs#L33-L36): `provider` не экранируется в `GetAuthorizationUrl`; на Windows URL уходит в `cmd /c start` ([AuthClient.cs:204-213](AvaluxAuth.UserClient/AuthClient.cs#L204-L213)) → command injection, если `provider` из недоверенного источника. Использовать `Uri.EscapeDataString`, `Process.Start` с `UseShellExecute=true` без `cmd`.

### C-02. UserClient: нет `state`/PKCE → login CSRF — ИСПРАВЛЕНО В КОДЕ (H-05)

`GetAuthorizationUrl` не отправлял `state`, а `ReceiveAuthCodeAsync` ([AuthClient.cs:216-240](AvaluxAuth.UserClient/AuthClient.cs#L216-L240)) его не проверял. Вредоносная страница открывала `http://localhost:14887/?code=<код атакующего>` — CLI логинился в чужой аккаунт.

**Исправление (2026-11-18, версия пакета 1.4.0).** `GetAuthorizationUrl`/`GetLinkUrl` генерируют CSPRNG `state` и PKCE S256 (`code_challenge`/`code_verifier`); verifier остаётся в процессе, на сервер уходит только challenge. Loopback-сервер отдаёт код вызывающему только при совпадении `state`, иначе возвращает 400 и `Authorization failed`; в `AuthorizeInstalledAsync`/`LinkInstalledAsync` проверка выполняется до обмена кода. `redirect_uri` передаётся при обмене, сервер сверяет его с сохранённым. `provider` экранируется через `Uri.EscapeDataString`. Остаточное (не входило в C-02): listener по-прежнему слушает `localhost` (не `127.0.0.1`) и не ограничивает path — рекомендовано отдельным hardening; command injection на Windows через `cmd /c start` — C-01.

### C-03. TestCli

`clientSecret` передаётся аргументом командной строки ([Arguments.cs](AvaluxAuth.TestCli/Arguments.cs)) — утечка в history/process list; credentials пишутся в plaintext JSON ([CredentialsStore.cs](AvaluxAuth.TestCli/CredentialsStore.cs)) — ограничить права/использовать keychain.

---

## 6. Зависимости

`dotnet list package --vulnerable --include-transitive` (API):

| Пакет | Сейчас | Уязвимость | Fixed |
|---|---|---|---|
| SixLabors.ImageSharp | 3.1.12 | 4 high + 2 moderate (TIFF decoder OOB/DoS и др.) | **3.2.0** (+ Drawing 2.2.0) |
| Microsoft.OpenApi (transitive) | 2.4.1 | GHSA-v5pm-xwqc-g5wc, high | **2.7.5** |
| AWSSDK.Core (transitive) | 4.0.0.5 | GHSA-9cvc-h2w8-phrp, low | **4.0.3.3** |

`npm audit`:

- **UI (админка):** 22 уязвимости — 13 high, 9 moderate (Angular core/common/compiler/router 21.1.4; fixed **21.2.17+**; moment, monaco/dompurify). Админка за VPN, шаблоны статические — прямая эксплуатация ограничена, но обновить нужно.
- **AuthUI (публичная форма):** 0 уязвимостей.
- Рекомендация: обновить lockfile, включить Dependabot/Renovate, `npm audit`/`dotnet list --vulnerable` в CI.

---

## 7. Что сделано хорошо

- CSPRNG (`RandomNumberGenerator`) для client id/secret, кодов, state, link/refresh-токенов; достаточная энтропия.
- BCrypt для паролей.
- Точное сравнение `redirect_uri` со списком (open redirect нет).
- JWT: подпись RS256, `ValidateIssuer`, `ValidateLifetime`; приватные ключи шифруются DataProtection.
- Admin-cookie: `HttpOnly`, `Secure`, `SameSite=Lax`, без `Domain`.
- Тенант-изоляция service-account эндпоинтов реализована (проверки `HasApplication`/`ApplicationId`).
- Нет raw SQL/конкатенации (EF параметризован), нет path traversal/SSRF из недоверенного ввода, нет опасной десериализации.
- Svelte-UI экранирует вывод; в Angular нет `bypassSecurityTrust`/`innerHTML`. В git-истории секретов не найдено.
- Провайдерские access/refresh-токены в БД зашифрованы, наружу refresh не отдаётся.

---

## 8. План исправлений

**P0 (срочно):**

1. ~~Закрыть `/api/v1/admin` на публичном хосте независимо от регистра.~~ **Сделано (H-01).**
2. ~~Экранировать HTML в `ErrorHtml` / убрать HTML-ошибки и `ex.Message` (H-02).~~ **Сделано в коде (H-02), ждёт деплоя.**
3. Обновить ImageSharp 3.2.0 + Drawing 2.2.0; ограничить размеры изображений; закрыть анонимную загрузку upload-токеном/state + rate limit (H-03, M-01). — **пакеты обновлены (`5146349`), загрузка закрыта state/квотами/очисткой (`f6651e7`, M-01); остаётся лимит на размеры изображений.**
4. ~~Включить реальную проверку отзыва service-account токенов (`OnTokenValidated`), исправить `TokenRepository` (H-04).~~ **Сделано в коде (H-04), ждёт деплоя.**
5. Ввести rate limiting + потребление state + lockout; серверная политика паролей (H-06).
6. ~~Определиться с client authentication: secret для конфиденциального клиента, привязка code/refresh к клиенту, PKCE (H-05).~~ **Сделано в коде (H-05), ждёт деплоя и обновления десктоп-клиентов.**
7. ~~Вынести Redis/Postgres в internal-сеть, включить auth/TLS для Redis (H-07).~~ **Сделано 2026-11-19** (TLS также для Postgres, least-privilege DB-роль).

**P1 (неделя):** ~~фильтр `StartsAt`~~ **сделано (M-02, `6dfe8c0`)**; ~~revoke сессий при смене пароля/absolute expiry~~ **сделано (H-05 + M-03, `5090e0b`)**; ротация ключей с окном; security-заголовки; убрать PII/секреты из логов; 401 вместо 500 на state; `700` на keys; ~~убрать PII/секреты из логов~~ **сделано (M-05, `90a23db`)**; почистить redirect URI; constant-time/fail-closed admin login; unique index на `Passwords.Login`.

**P2:** Angular 21.2.17+, Microsoft.OpenApi 2.7.5+, AWSSDK 4.0.3.3+; ~~пагинация~~ **сделано (L-04, `30c2b78`)**; CI/digest/scanning; ~~`keys/` в ignore~~ **сделано (L-09, `aa34d17`)**; аудит-лог; клиентские библиотеки; defense-in-depth для админки на уровне приложения.

---

## 9. Методика и ограничения

- Полный статический разбор C#-проектов, EF-миграций, обоих фронтендов, Docker/CI, конфигурации стека и файловой системы контейнера.
- `dotnet list package --vulnerable --include-transitive`, `npm audit`, сверка с OSV/NuGet.
- Безопасные live-проверки: OIDC discovery, невалидный state, `color` вне диапазона, POST без файла, варианты регистра админ-пути, один неверный admin-логин; XSS подтверждён инертным payload'ом.
- Исправления 2026-11-19 проверялись отдельно: M-05 — статическим `grep` и прогоном санитайзера, L-04/L-05 — одноразовым стендом (чистая БД в dev-стеке `reportchecker-dev` с миграциями + локальный запуск API для HTTP-проверок пагинации, провайдер с подменённым `HttpMessageHandler` для Google); стирал ли L-09 key ring, проверено `git check-ignore`/`git status` после реального запуска. Живой контур `auth` не затрагивался, тестовая БД удалена, временные сборки — в `/tmp`.
- **Не выполнялись:** брутфорс, разрушительные действия, массовое сканирование, выгрузка секретов. Полноценный пентест бизнес-логики не проводился.
- Значения секретов, `client_id`, state и токены в отчёт не включались.

---

## 10. Приложение: проверка H-01 после фикса

| Проверка | Результат |
|---|---|
| 32 регистровых варианта `admin` | 404 |
| `POST /api/v1/ADMIN/login` | 404 |
| Обходы с `%`-кодированием, `..`, `//`, `;`, `.` | 404 |
| `.well-known`, `/login`, `/profile`, `/static/*`, `/api/v1/avatar` | 200 |
| `POST /api/v1/avatar` без файла | 400 |
| `/api/v1/service/users` без токена | 401 |
| `POST /api/v1/password/signin`, `POST /api/v1/auth/token` | 400 |
| `auth-admin.reportchecker.ru` без VPN | 403 |

**Вывод:** публичный обход админки закрыт; публичные маршруты и VPN-контур админки не затронуты. Изменение выполнено в Portainer-стеке `auth`, в git не коммитилось.
