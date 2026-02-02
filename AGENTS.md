# AGENTS

Краткие инструкции по работе с репозиторием `ava-shell-proj`.

## Назначение
Набор сервисов для аутентификации/авторизации и "safe" домена, с API Gateway, Nginx reverse proxy и административным UI.
Продукт разворачивается на одном домене клиента (например `1.ava-kk.ru`) без поддоменов для отдельных сервисов.
Auth должен работать внутри того же домена (обычно под префиксом `/auth`).

## Структура репозитория
- `Api.Gateway/` — .NET API Gateway (порт 8080 в контейнере).
- `Auth.Service/` — .NET Auth/OIDC сервис (порты 8080/8081 в контейнере).
- `Safe.Service/` — .NET Safe сервис (порт 5001 в контейнере).
- `computeradmin-ui/` — React/Vite админ UI (порт 5000 в контейнере, dev-сервер 3000).
- `nginx/` — reverse proxy + SSL терминация.

### Структура Auth.Service
- `Auth.Domain/` — доменные сущности (Employee, RoleScope, Token/Session пока здесь).
- `Auth.Application/` — сервисы и use‑cases.
- `Auth.Infrastructure.Persistence/` — EF Core, миграции, репозитории, UnitOfWork.
- `Auth.Infrastructure.Oidc/` — OIDC протокол (endpoints, PKCE, signing keys).
- `Auth.Infrastructure.Telegram/` — Telegram интеграция (валидация, bind/unbind, модели).
- `Auth.Host/` — HTTP pipeline, DI, Razor Pages.

## Единый домен и /auth
Так как всё работает на одном домене клиента, `Auth.Service` публикуется через тот же домен под префиксом `/auth`.
Важно сохранять префикс `/auth` в редиректах и callback URL.

## API префиксы
- Auth internal API: `/api/auth/*`
- Safe API: `/api/safe/*`
- Gateway admin: `/api/gateway/*`

## Конфигурация и env
В каждом сервисе есть свой `.env` (не коммитится). Не публикуйте секреты.
- `Safe.Service/.env.simple` — пример набора переменных для Safe.
- `computeradmin-ui/config.template.js` — шаблон runtime-конфига для UI.

UI ожидает переменные:
- `VITE_AUTH_API_BASE_URL`, `VITE_SAFE_API_BASE_URL`
- `VITE_OIDC_AUTHORITY`, `VITE_OIDC_CLIENT_ID`
- `VITE_OIDC_REDIRECT_URI`, `VITE_OIDC_POST_LOGOUT_REDIRECT_URI`
- `VITE_OIDC_SILENT_REDIRECT_URI`, `VITE_OIDC_SCOPE`

Nginx использует переменные (см. `nginx/nginx.conf.template` и `nginx/entrypoint.sh`):
- `PRIMARY_DOMAIN`, `EXTRA_DOMAINS`
- `API_UPSTREAM`, `AUTH_UPSTREAM`, `ADMIN_UPSTREAM`
Дополнительно для сертификатов: `CF_DNS_API_TOKEN`, `CERTBOT_EMAIL`, `CF_PROPAGATION_SECONDS`, `RENEW_INTERVAL`.

Auth.Service (см. `Auth.Service/Auth.Host/Program.cs`):
- `AUTH_PUBLIC_PORT` (по умолчанию 8080), `AUTH_INTERNAL_PORT` (по умолчанию 8081)
- `SWAGGER_ENABLED` — включает Swagger в non-dev
- `AUTH_REVERSE_PROXY`/`FORWARDED_PROXY`/`NGINX_FORWARDER` и `AUTH_REVERSE_PROXY_NETWORK` — доверенные форвардеры
- `AUTH_TRUST_ALL_FORWARDERS` — доверять всем (осторожно)
- `AUTH_PATH_BASE` — базовый префикс пути для Auth (например `/auth`), нужен если Auth проксируется под префиксом
Rate limit на публичных эндпоинтах Auth: 5 попыток за 3 секунды, блок на 10 секунд (429).
Клиенты OIDC задаются через переменные окружения (секция `AuthClients`), хранятся в памяти экземпляра.
Обычно под каждого клиента отдельный экземпляр/сабдомен и отдельная БД/схема.
OIDC signing key статичный, ротация не требуется (зафиксировать через `OIDC__SIGNING_KEY` или `OIDC__SigningKeyPath`).
Telegram-авторизация опциональна: можно оставить для проды, но функциональность не критична.

Api.Gateway (см. `Api.Gateway/Program.cs`, `Api.Gateway/GatewayOptions.cs`):
- `Gateway:ConnectionString` — БД для эндпоинтов/пермишенов
- `Gateway:AuthSwaggerUrl`, `Gateway:SafeSwaggerUrl` — прокси контрактов
- `Gateway:RoleScopesSyncUrl`, `Gateway:GatewaySyncToken` — синхронизация role scopes
`GatewaySyncToken` сейчас задаётся вручную (секрет), позже будет продуман механизм генерации.
 - `Gateway:AuthIntrospectionUrl` — introspection endpoint Auth.Service
 - `Gateway:IntrospectionClientId`, `Gateway:IntrospectionClientSecret` — клиент для introspection
 - `Gateway:Routes` — список правил роутинга (`PathPrefix`, `Scope`, `Upstream`)
 - `Gateway:PathBase` — базовый префикс пути для gateway (например `/api`)
 - `Gateway:PolicyRefreshSeconds` — период обновления policy/permissions
 - `Gateway:RevokedCacheMinutes` — TTL кэша отозванных токенов
Формат `Gateway:Routes` в env:
```
Gateway__Routes__0__PathPrefix=/api/
Gateway__Routes__0__Scope=auth
Gateway__Routes__0__Upstream=http://auth.service:8080
```

## Api.Gateway поведение
- Все запросы проходят через `MapProxyEndpoint` и требуют Bearer токен.
- Токен валидируется через OIDC introspection; при отсутствии/ошибке интроспекции запросы отклоняются.
- Доступ проверяется по scope (role scopes) и per-endpoint permissions; root имеет полный доступ.
- Endpoint-список хранится в БД и синкается из `EndpointCatalog` при старте.
- Role scopes подтягиваются из Auth.Service через `RoleScopesSyncUrl` + `X-Gateway-Token`.
- Proxy добавляет заголовки `X-User-Id`, `X-User-Name`, `X-Gateway-Token` (для auth upstream).
- HttpClient в gateway принимает любой TLS сертификат (RemoteCertificateValidationCallback = true).
  Это удобно в dev, но небезопасно в prod.


## Запуск через Docker
Каждый сервис имеет свой `docker-compose.yml`.
Перед запуском нужен внешний network `nginx_network`, а также `api_gateway` для сервисов, которые к нему подключаются.

Примеры:
```bash
docker network create nginx_network || true
docker network create api_gateway || true

docker compose -f nginx/docker-compose.yml up -d --build
docker compose -f Api.Gateway/docker-compose.yml up -d --build
docker compose -f Auth.Service/docker-compose.yml up -d --build
docker compose -f Safe.Service/docker-compose.yml up -d --build
docker compose -f computeradmin-ui/docker-compose.yml up -d --build
```

Порты снаружи:
- Nginx: 80/443
- UI: 5000 (если запускать напрямую, минуя Nginx)

## Health и swagger
- Gateway: `GET /healthz`, swagger: `GET /api/gateway/swagger/gateway-admin/swagger.json`
- Auth.Service: `GET /health` только на internal порту (Swagger тоже только internal)
- Safe.Service: `GET /health`

## Частые проблемы
- Нет сети `nginx_network` или `api_gateway` — контейнеры не стартуют.
- Ошибки конфигурации доменов и upstream в `nginx/nginx.conf.template`.
- Не заполнены обязательные `VITE_*` переменные — UI не сможет авторизоваться.
- Не настроены `AUTH_*` переменные для reverse proxy — могут ломаться редиректы в OIDC.
- Internal порт Auth.Service должен быть закрыт извне и доступен только из сети (API Gateway).
