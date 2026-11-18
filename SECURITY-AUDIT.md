# Аудит безопасности AvaluxAuth

- **Дата:** 2026-11-18 (Europe/Moscow); исправление H-07 выкачено 2026-11-19.
- **Ревизия:** `ab6731d` (`master`) — аудит; `9c6575c` — код H-07 (валидация TLS Redis по приватному CA); `4a09914` — код H-06 (rate limiting/lockout, сжигание state, политика пароля). На живом контуре — образ `c4c9617` (см. H-07, раздел «Порядок деплоя»).
- **Область:** `Api/*` (API, сервисы, провайдеры, DataAccess, S3), `AuthUI`, `UI`, Docker/CI, клиентские библиотеки, живой контур в Portainer (стек `auth`).
- **Статус:** H-01 и H-07 исправлены; H-02, H-04, H-05 и H-06 исправлены в коде, но ещё не задеплоены на живой контур; H-03 открыт.
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
| M-01 | Medium | Анонимная запись в S3 без квот и очистки | Открыто |
| M-02 | Medium | Будущие подписки выдаются сразу (`StartsAt` не учитывается) | Открыто |
| M-03 | Medium | Смена пароля не инвалидирует сессии; refresh-токены бессрочные | Открыто |
| M-04 | Medium | Ротация signing key не инвалидирует старые ключи/JWKS | Открыто |
| M-05 | Medium | Секреты и PII в логах (`Console.WriteLine`) | Открыто |
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
| L-04 | Low | Пагинация: неверный порядок `Take/Skip`, нет верхней границы `limit` | Открыто |
| L-05 | Low | Google refresh отправляет `grant_type=authorization_code` | Открыто |
| L-06 | Low | `ApiClient.GetAccessTokenAsync` игнорирует provider | Открыто |
| L-07 | Low | Приложение работает под суперпользователем Postgres | **Исправлено (H-07)** |
| L-08 | Low | CI: лишние права, теги вместо SHA/digest, нет scan/SBOM | Открыто |
| L-09 | Low | `keys/` не в `.gitignore`/`.dockerignore` | Открыто |
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

**Остаточный риск.** Глобальных security-заголовков по-прежнему нет (M-06) — защищена только страница ошибки. Сырой `error_description` теперь попадает в логи, поэтому санитайзинг логов остаётся в рамках M-05.

---

### H-03. Уязвимый ImageSharp на анонимной загрузке изображений

**Где:** [AvaluxAuth.Services.csproj:22](Api/AvaluxAuth.Services/AvaluxAuth.Services.csproj#L22) — `SixLabors.ImageSharp 3.1.12`; [AvatarController.cs:22-34](Api/AvaluxAuth.Api/Controllers/AvatarController.cs#L22-L34) — `[Authorize]` отсутствует; [ImageService.cs:111-113](Api/AvaluxAuth.Services/ImageService.cs#L111-L113) — `Image.LoadAsync` на пользовательском потоке.

**Уязвимости 3.1.12 (OSV/NuGet на 2026-11-18):**

- **GHSA-v76p-62qx-wwq2** (high): tiled fax TIFF — heap OOB write в декодере; fixed **3.2.0**;
- **GHSA-wmxv-xphr-5c9g** (moderate): BigTIFF IFD count — зависание декодера; fixed 3.2.0;
- **GHSA-j3p4-wp97-rph4 / j9gm-c75j-xc9q / jjfr-hcj7-qf5w** (high): HistogramEqualization и TIFF-энкодеры; fixed 3.2.0. В текущем коде не задействованы, но пакет уязвим.

Декодер TIFF достижим **без аутентификации** через `POST /api/v1/avatar`; лимит 10 МБ не защищает от decompression bomb и крафтовых TIFF. Даже «только крэш» с `restart: always` даёт удалённый DoS.

**Исправление.** Обновить `SixLabors.ImageSharp` до **3.2.0**, `SixLabors.ImageSharp.Drawing` до **2.2.0** (зависит от 3.2.0). Ограничить пиксели/размеры (`DecoderOptions`, проверка `Width*Height`). Требовать короткоживущий upload-токен/state для загрузки, rate limit, квоты, очистку сиротских аватаров и bucket lifecycle.

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

**Остаточный риск.** Пользовательские access-токены (1 час) не имеют `jti`/серверной записи и по-прежнему не отзываются — вне объёма H-04 (см. M-03). Срок жизни service-account токена задаётся админом без верхней границы; отзыв теперь работает, но украденный токен остаётся валидным до отзыва/истечения. Проверка добавляет один запрос к БД на service-account запрос при промахе кэша; ротация ключей/JWKS — M-04.

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
- Смена пароля по-прежнему не инвалидирует refresh-токены — M-03.

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

### M-01. Анонимная запись в S3 без квот и очистки

[AvatarController.cs:22-34](Api/AvaluxAuth.Api/Controllers/AvatarController.cs#L22-L34): загрузка без auth, 10 МБ на файл. `ClearFilesCreatedBefore` нигде не вызывается ([S3Repository.cs:194](Api/AvaluxAuth.S3/S3Repository.cs#L194)) — бесплатный хостинг картинок и рост storage/cost. Нужны upload-токен/state, rate limit, квоты, lifecycle/cleanup.

### M-02. Будущие подписки выдаются сразу

[UserRepository.cs:170](Api/AvaluxAuth.DataAccess/Repositories/UserRepository.cs#L170) и [:192](Api/AvaluxAuth.DataAccess/Repositories/UserRepository.cs#L192) фильтруют только `ExpiresAt > UtcNow`, но не `StartsAt <= UtcNow`. Подписка, запланированная на будущее, уже попадает в claim `Subscriptions` JWT. Добавить проверку `StartsAt`.

### M-03. Смена пароля не инвалидирует сессии; refresh-токены бессрочные

[PasswordService.cs](Api/AvaluxAuth.Services/PasswordService.cs) меняет только хеш; [RefreshTokenEntity.cs](Api/AvaluxAuth.DataAccess/Entities/RefreshTokenEntity.cs) без `CreatedAt/ExpiresAt/ClientId`. После компрометации/смены пароля старые refresh-токены живут вечно. Добавить absolute expiry, client binding и revoke-all при смене пароля/линковке.

### M-04. Ротация signing key не инвалидирует старые ключи

[SigningKeyRepository.cs](Api/AvaluxAuth.DataAccess/Repositories/SigningKeyRepository.cs): `AddAsync` деактивирует старый ключ, но `GetAllAsync`/JWKS отдают его вечно, `ExpiresAt` не проверяется. Утёкший старый приватный ключ остаётся валидным. Публиковать только ключи в окне overlap и удалять/истекать старые.

### M-05. Секреты и PII в логах

[GoogleAuthProvider.cs:24,32](Api/AvaluxAuth.Providers/GoogleAuthProvider.cs#L24) — полный authorization URL со `state`; [MicrosoftAuthProvider.cs:204](Api/AvaluxAuth.Providers/MicrosoftAuthProvider.cs#L204) — фото пользователя в base64; [OauthService.cs:50,55](Api/AvaluxAuth.Services/OauthService.cs#L50); [ImageService.cs:66](Api/AvaluxAuth.Services/ImageService.cs#L66). Убрать `Console.WriteLine`, использовать `ILogger` с редактированием.

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
- **L-04. Пагинация.** [UserRepository.cs:105-106](Api/AvaluxAuth.DataAccess/Repositories/UserRepository.cs#L105-L106): `Take(limit).Skip(page*limit)` — неверный порядок; `limit` без верхней границы; отрицательный `page` → 500.
- **L-05. Google refresh.** [GoogleAuthProvider.cs](Api/AvaluxAuth.Providers/GoogleAuthProvider.cs) отправляет `grant_type=authorization_code` вместо `refresh_token` — refresh Google не работает.
- **L-06. ApiClient.** `GetAccessTokenAsync` игнорирует `provider` — запрос без `providerKey` вернёт 400.
- **L-07. DB-суперпользователь.** ~~Приложение работает под `POSTGRES_USER`.~~ **Исправлено вместе с H-07:** рантайм работает под `avaluxauth_app`; суперпользователь остаётся только для initdb/админских задач.
- **L-08. CI.** `.github/workflows/main.yml` даёт `contents: write`, образы/actions не запинены по digest/SHA, сборка на каждый push; нет Trivy/SBOM/подписи.
- **L-09. `keys/`.** Не в `.gitignore`/`.dockerignore`. Локальный запуск создаёт key ring; добавить `**/keys/`.
- **L-10. Наблюдаемость.** Нет аудит-лога админских действий и централизованного хранения логов; у `auth` нет healthcheck/resource limits. Неудачные входы с 2026-11-19 логируются в stdout (H-06), но только с ротацией 1m.
- **L-11. Avatar GET.** `text` до ~8 КБ и рендер шрифтом 100px — CPU/память DoS; `colorIndex` вне диапазона → 500.

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
3. Обновить ImageSharp 3.2.0 + Drawing 2.2.0; ограничить размеры изображений; закрыть анонимную загрузку upload-токеном/state + rate limit (H-03, M-01).
4. ~~Включить реальную проверку отзыва service-account токенов (`OnTokenValidated`), исправить `TokenRepository` (H-04).~~ **Сделано в коде (H-04), ждёт деплоя.**
5. Ввести rate limiting + потребление state + lockout; серверная политика паролей (H-06).
6. ~~Определиться с client authentication: secret для конфиденциального клиента, привязка code/refresh к клиенту, PKCE (H-05).~~ **Сделано в коде (H-05), ждёт деплоя и обновления десктоп-клиентов.**
7. ~~Вынести Redis/Postgres в internal-сеть, включить auth/TLS для Redis (H-07).~~ **Сделано 2026-11-19** (TLS также для Postgres, least-privilege DB-роль).

**P1 (неделя):** фильтр `StartsAt`; revoke сессий при смене пароля/absolute expiry; ротация ключей с окном; security-заголовки; убрать PII/секреты из логов; 401 вместо 500 на state; `700` на keys; почистить redirect URI; constant-time/fail-closed admin login; unique index на `Passwords.Login`.

**P2:** Angular 21.2.17+, Microsoft.OpenApi 2.7.5+, AWSSDK 4.0.3.3+; пагинация; CI/digest/scanning; `keys/` в ignore; аудит-лог; клиентские библиотеки; defense-in-depth для админки на уровне приложения.

---

## 9. Методика и ограничения

- Полный статический разбор C#-проектов, EF-миграций, обоих фронтендов, Docker/CI, конфигурации стека и файловой системы контейнера.
- `dotnet list package --vulnerable --include-transitive`, `npm audit`, сверка с OSV/NuGet.
- Безопасные live-проверки: OIDC discovery, невалидный state, `color` вне диапазона, POST без файла, варианты регистра админ-пути, один неверный admin-логин; XSS подтверждён инертным payload'ом.
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
