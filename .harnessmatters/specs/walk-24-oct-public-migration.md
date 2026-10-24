---
harness_matters: spec/v1
spec_id: f8a9f297-c3f5-46c8-aa6c-45f005131385
version: 1
content_hash: 5b848541f1ee75d4
workspace_id: 5f1a74b0-4a9b-4ffc-859f-33126dca1c80
published_at: 2026-10-24T05:14:47.014589+00:00
---
# Walk 24 Oct public migration

## Intent
### Intent

Migrate sandeep-chanda/piggymetricsJava to sandeep-chanda/PiggyMetricsDotNet, keeping the services.

### Pre-conditions

- ends · piggymetricsJava → PiggyMetricsDotNet · separate_repo · PiggyMetricsDotNet is not indexed

## Decisions
```decision
chosen: Spring Cloud → ASP.NET Core | the destination is .NET; the source stack is Spring Boot / Spring Cloud
chosen: Eureka → none | service URLs are configured, not discovered
chosen: Feign → HttpClient | keep every HTTP edge with its path and media type, drop the interface proxy
chosen: Zuul → YARP | the same prefix routes; a direct-URL route stays a direct URL
chosen: Spring Security OAuth → ASP.NET Identity | the same grants and scopes on the destination's own identity stack
chosen: Hystrix → Polly | the same timeout and fallback per edge as a policy on the typed client
chosen: Spring Cloud Config → appsettings.json + environment | configuration per service; secrets from the environment; no config server to start first
chosen: Turbine → OpenTelemetry | metrics per service to a collector; no stream aggregator
chosen: runtime · .NET 8 LTS · ASP.NET Core · one project per runtime service plus Shared | the .NET default; the Brief's Target shape line changes it · keeps: each service deploys and scales alone, as the source's do; Shared is the only coupling
chosen: gateway · YARP · the same route prefixes as static clusters · static UI served from the gateway host | the .NET default; the Brief's Target shape line changes it · keeps: the route table lives in the gateway project's configuration; the static UI moves host only
chosen: discovery · none · service addresses are configuration | the .NET default; the Brief's Target shape line changes it · keeps: every callee address is a setting; adding an instance changes configuration, not a registry
chosen: configuration · appsettings.json per service + environment variables for secrets · the config server is retired | the .NET default; the Brief's Target shape line changes it · keeps: the config module is retired; each service owns its settings file and reads secrets from the environment
chosen: calls · typed HttpClient per edge · Polly timeout matching the source · the same failure behaviour per edge | the .NET default; the Brief's Target shape line changes it · keeps: each edge keeps the source's timeout and failure behaviour in code the service owns
chosen: persistence · MongoDB.Driver on the same databases and collections · same BSON field names and _id semantics | the .NET default; the Brief's Target shape line changes it · keeps: the destination reads and writes the source's collections in place; no data migration and no rollback of data
chosen: auth · OpenIddict issuing the same grants and scopes · a user-info endpoint in the shape the resource servers read | the .NET default; the Brief's Target shape line changes it · keeps: tokens keep the source's grants and scopes; resource servers validate the way they do today
chosen: resilience · Polly · ASP.NET health checks · OpenTelemetry · dashboard and metrics stream retired | the .NET default; the Brief's Target shape line changes it · keeps: the dashboard and metrics stream are retired; health and telemetry come from the platform
chosen: scheduling · hosted BackgroundService per cron · MailKit over the same SMTP settings | the .NET default; the Brief's Target shape line changes it · keeps: each cron runs inside its service as a hosted service; no scheduler store to migrate
rejected: Keep the services on Spring Boot and only re-host them | no rewrite, the Java build keeps working | the migration's destination is a .NET repository — the Brief set it
rejected: Run a discovery client on the destination (Steeltoe) | keeps the registry the source already runs | carries a Java registry into a .NET estate for nine fixed services
rejected: Generate typed clients from the source controllers | one client per contract, checked at build time | no contract file exists in the source index to generate from
rejected: Keep the Java gateway in front of the destination services | no route change during the waves | a Java hop stays live after Cutover
rejected: Keep the Java auth service as the token issuer | no re-issue of tokens during the waves | one source module survives Cutover
rejected: No breaker on the destination; let the call fail | one fewer library | the fallback behaviour the source's callers rely on
rejected: Run a config server on the destination | keeps the shared yaml layout | a start-order dependency the source's fail-fast makes fatal
rejected: Keep the Java dashboard reading the destination | no new tooling | a Java module that outlives the cutover
rejected: one monolith project · a worker per service without ASP.NET Core | one process to build, run and debug; no call between services to fail | gives up that each service deploys and scales alone, as the source's do; Shared is the only coupling
rejected: Ocelot · an edge proxy outside the solution (nginx, Envoy) | a proxy the platform already runs; no gateway code to port | gives up that the route table lives in the gateway project's configuration; the static UI moves host only
rejected: Consul · Kubernetes DNS · Steeltoe discovery | instances register themselves; addresses are not settings to keep in step | gives up that every callee address is a setting; adding an instance changes configuration, not a registry
rejected: a config service (Azure App Configuration, Consul KV) · keep the config server | one place changes a setting for every service, as the source does today | gives up that the config module is retired; each service owns its settings file and reads secrets from the environment
rejected: Refit typed clients · gRPC (changes the contract) · a service mesh for timeouts | typed clients or a mesh take the retry and timeout code out of each service | gives up that each edge keeps the source's timeout and failure behaviour in code the service owns
rejected: EF Core with a Mongo provider · a relational store (changes the data map) | a store or an ORM the team already knows, with its migration tooling | gives up that the destination reads and writes the source's collections in place; no data migration and no rollback of data
rejected: Duende IdentityServer (commercial licence) · an external identity provider (changes the token endpoint) | a supported identity product; issuing and rotating tokens is not this codebase's to maintain | gives up that tokens keep the source's grants and scopes; resource servers validate the way they do today
rejected: Steeltoe · keep the dashboard and metrics stream on the source until cutover | the dashboard and metrics stream keep working on the source until cutover; nothing to rebuild first | gives up that the dashboard and metrics stream are retired; health and telemetry come from the platform
rejected: Quartz.NET · Hangfire · a platform cron calling an endpoint | a scheduler with a store survives restarts and spreads jobs across instances | gives up that each cron runs inside its service as a hosted service; no scheduler store to migrate
```

## Source facts
### Summary

piggymetricsJava is 9 Maven modules (account-service, auth-service, config, gateway, monitoring, notification-service, registry, statistics-service, turbine-stream-service) · 97 Java files on spring-boot-starter-parent 2.0.3.RELEASE · Java 1.8. 5 runtime services carry behaviour (gateway, auth-service, account-service, statistics-service, notification-service); 4 infrastructure modules exist for the platform (config (config-server), monitoring (dashboard), registry (registry), turbine-stream-service (metrics-stream)) and become platform decisions, not units. The read states 12 HTTP endpoints, 4 stored collections, 4 outbound clients, 2 scheduled jobs, 4 OAuth clients and 61 tests in 25 classes. PiggyMetricsDotNet is the destination on .NET: it is written by this spec as proposals (the Target shape, the contract and data mappings), never read as evidence.

### Inventory

- piggymetricsJava · 9 modules · 72 main files · 5 runtime services · 4 infrastructure modules → PiggyMetricsDotNet · .NET · proposed
- config · infrastructure · config-server · 2 files → retire · configuration per service · default
- monitoring · infrastructure · dashboard · 1 file → retire · health checks and metrics · default
- registry · infrastructure · registry · 1 file → retire · addresses are configuration · default
- gateway · runtime · 0 endpoints · 1 file · 2 tests → Gateway · proposed
- auth-service · runtime · 3 endpoints · 9 files · 9 tests → AuthService · proposed
- account-service · runtime · 4 endpoints · 17 files · 14 tests → AccountService · proposed
- statistics-service · runtime · 3 endpoints · 23 files · 16 tests → StatisticsService · proposed
- notification-service · runtime · 2 endpoints · 17 files · 18 tests → NotificationService · proposed
- turbine-stream-service · infrastructure · metrics-stream · 1 file → retire · health checks and metrics · default

### Services

- config · infrastructure · config-server · port 8888 · no tests
- monitoring · infrastructure · dashboard · 1 test
- registry · infrastructure · registry · port 8761 · no tests
- gateway · runtime · gateway · port 4000 · 2 tests
- auth-service · runtime · port 5000 · /uaa · users on auth-mongodb · 3 endpoints · 1 collection · authorization server · 9 tests
- account-service · runtime · port 6000 · /accounts · accounts on account-mongodb · 4 endpoints · 1 collection · 2 clients · resource server · 14 tests
- statistics-service · runtime · port 7000 · /statistics · datapoints on statistics-mongodb · 3 endpoints · 1 collection · 1 client · resource server · 16 tests
- notification-service · runtime · port 8000 · /notifications · recipients on notification-mongodb · 2 endpoints · 1 collection · 1 client · 2 jobs · resource server · 18 tests
- turbine-stream-service · infrastructure · metrics-stream · 1 test

### Contract

- C1 · GET /uaa/users/current · user → Principal · UserController.getUser · auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L22
- C2 · POST /uaa/users · server · User (validated) → void · UserController.createUser · auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L28
- C3 · POST /uaa/oauth/token · client basic auth · grant_type=client_credentials | password | refresh_token → token · framework · TokenEndpoint
- C4 · GET /accounts/{name} · server, or name == demo → Account · AccountController.getAccountByName · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L20
- C5 · GET /accounts/current · user → Account · AccountController.getCurrentAccount · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L25
- C6 · PUT /accounts/current · user · Account (validated) → void · AccountController.saveCurrentAccount · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L30
- C7 · POST /accounts · open · User (validated) → Account · AccountController.createNewAccount · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L35
- C8 · GET /statistics/current · user → List<DataPoint> · StatisticsController.getCurrentAccountStatistics · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L20
- C9 · GET /statistics/{accountName} · server, or accountName == demo → List<DataPoint> · StatisticsController.getStatisticsByAccountName · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L26
- C10 · PUT /statistics/{accountName} · server · Account (validated) → void · StatisticsController.saveAccountStatistics · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L32
- C11 · GET /notifications/recipients/current · user → Object · RecipientController.getCurrentNotificationsSettings · notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L21
- C12 · PUT /notifications/recipients/current · user · Recipient (validated) → Object · RecipientController.saveCurrentNotificationsSettings · notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L26

### Data

- users on auth-mongodb · User · _id = username · username: String; password: String · auth-service/src/main/java/com/piggymetrics/auth/domain/User.java:L10
- accounts on account-mongodb · Account · _id = name · name: String; lastSeen: Date; incomes: [Item{title, amount, currency, period, icon}] · @Valid; expenses: [Item{title, amount, currency, period, icon}] · @Valid; saving: Saving{amount, currency, interest, deposit, capitalization} · @Valid @NotNull; note: String · @Length(min = 0, max = 20_000) · account-service/src/main/java/com/piggymetrics/account/domain/Account.java:L13
- accounts · request shape, no repository saves it · Account · incomes: [Item{title, amount, currency, period}] · @Valid @NotNull; expenses: [Item{title, amount, currency, period}] · @Valid @NotNull; saving: Saving{amount, currency, interest, deposit, capitalization} · @Valid @NotNull · statistics-service/src/main/java/com/piggymetrics/statistics/domain/Account.java:L10
- datapoints on statistics-mongodb · DataPoint · _id = id · id: DataPointId{serialVersionUID, account, date}; incomes: [ItemMetric{title, amount}]; expenses: [ItemMetric{title, amount}]; statistics: Map<StatisticMetric, BigDecimal>; rates: Map<Currency, BigDecimal> · statistics-service/src/main/java/com/piggymetrics/statistics/domain/timeseries/DataPoint.java:L15
- recipients on notification-mongodb · Recipient · _id = accountName · accountName: String; email: String · @NotNull @Email; scheduledNotifications: NotificationSettings{active, frequency, lastNotified} · @Valid · notification-service/src/main/java/com/piggymetrics/notification/domain/Recipient.java:L11

### Edges

- E1 · account-service → auth-service · @FeignClient POST /uaa/users · server · propagates · account-service/src/main/java/com/piggymetrics/account/client/AuthServiceClient.java:L9
- E2 · account-service → statistics-service · @FeignClient PUT /statistics/{accountName} · server · fallback · StatisticsServiceClientFallback · account-service/src/main/java/com/piggymetrics/account/client/StatisticsServiceClient.java:L10
- E3 · statistics-service → ${rates.url} · @FeignClient GET /latest · none · fallback · ExchangeRatesClientFallback · statistics-service/src/main/java/com/piggymetrics/statistics/client/ExchangeRatesClient.java:L10
- E4 · notification-service → account-service · @FeignClient GET /accounts/{accountName} · server · caught and logged in NotificationServiceImpl · notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java:L9
- E5 · monitoring → config · spring.cloud.config.uri http://config:8888 · basic · fails to start · monitoring/src/main/resources/bootstrap.yml:L6
- E6 · registry → config · spring.cloud.config.uri http://config:8888 · basic · fails to start · registry/src/main/resources/bootstrap.yml:L6
- E7 · gateway → auth-service · zuul.routes /uaa/** · pass-through · by url · config/src/main/resources/shared/gateway.yml:L21
- E8 · gateway → account-service · zuul.routes /accounts/** · pass-through · by serviceId · config/src/main/resources/shared/gateway.yml:L27
- E9 · gateway → statistics-service · zuul.routes /statistics/** · pass-through · by serviceId · config/src/main/resources/shared/gateway.yml:L33
- E10 · gateway → notification-service · zuul.routes /notifications/** · pass-through · by serviceId · config/src/main/resources/shared/gateway.yml:L39
- E11 · gateway → config · spring.cloud.config.uri http://config:8888 · basic · fails to start · gateway/src/main/resources/bootstrap.yml:L6
- E12 · gateway → registry · @EnableDiscoveryClient · none · registers
- E13 · auth-service → config · spring.cloud.config.uri http://config:8888 · basic · fails to start · auth-service/src/main/resources/bootstrap.yml:L6
- E14 · auth-service → registry · @EnableDiscoveryClient · none · registers
- E15 · account-service → config · spring.cloud.config.uri http://config:8888 · basic · fails to start · account-service/src/main/resources/bootstrap.yml:L6
- E16 · account-service → registry · @EnableDiscoveryClient · none · registers
- E17 · account-service → broker · spring-cloud-starter-bus-amqp, spring-cloud-netflix-hystrix-stream rabbitmq · none · metrics and bus only · config/src/main/resources/shared/application.yml:L27
- E18 · account-service → auth · user-info-uri http://auth-service:5000/uaa/users/current · bearer · invalid token → 401 · config/src/main/resources/shared/application.yml:L23
- E19 · statistics-service → config · spring.cloud.config.uri http://config:8888 · basic · fails to start · statistics-service/src/main/resources/bootstrap.yml:L6
- E20 · statistics-service → registry · @EnableDiscoveryClient · none · registers
- E21 · statistics-service → broker · spring-cloud-starter-bus-amqp, spring-cloud-netflix-hystrix-stream rabbitmq · none · metrics and bus only · config/src/main/resources/shared/application.yml:L27
- E22 · statistics-service → auth · user-info-uri http://auth-service:5000/uaa/users/current · bearer · invalid token → 401 · config/src/main/resources/shared/application.yml:L23
- E23 · notification-service → config · spring.cloud.config.uri http://config:8888 · basic · fails to start · notification-service/src/main/resources/bootstrap.yml:L6
- E24 · notification-service → registry · @EnableDiscoveryClient · none · registers
- E25 · notification-service → broker · spring-cloud-starter-bus-amqp, spring-cloud-netflix-hystrix-stream rabbitmq · none · metrics and bus only · config/src/main/resources/shared/application.yml:L27
- E26 · notification-service → auth · user-info-uri http://auth-service:5000/uaa/users/current · bearer · invalid token → 401 · config/src/main/resources/shared/application.yml:L23
- E27 · turbine-stream-service → config · spring.cloud.config.uri http://config:8888 · basic · fails to start · turbine-stream-service/src/main/resources/bootstrap.yml:L6
- E28 · turbine-stream-service → registry · @EnableDiscoveryClient · none · registers
- E29 · turbine-stream-service → broker · spring-cloud-starter-netflix-turbine-stream, spring-cloud-starter-stream-rabbit rabbitmq · none · metrics and bus only · config/src/main/resources/shared/application.yml:L27

### Non-functional requirements

- account-service · `spring.cloud.config.fail-fast: true` — fails fast when the config server is unreachable · fail-fast on · account-service/src/main/resources/bootstrap.yml:L7
- auth-service · `spring.cloud.config.fail-fast: true` — fails fast when the config server is unreachable · fail-fast on · auth-service/src/main/resources/bootstrap.yml:L7
- account-service · `spring.data.mongodb.host: account-mongodb` — the service's own store · config/src/main/resources/shared/account-service.yml:L13
- account-service · `feign.hystrix.enabled: true` — circuit breaker · config/src/main/resources/shared/account-service.yml:L26
- every service · `hystrix.command.default.execution.isolation.thread.timeoutInMilliseconds: 10000` — 10 s timeout · config/src/main/resources/shared/application.yml:L11
- every service · `spring.rabbitmq.host: rabbitmq` — message broker · config/src/main/resources/shared/application.yml:L27
- auth-service · `spring.data.mongodb.host: auth-mongodb` — the service's own store · config/src/main/resources/shared/auth-service.yml:L4
- gateway · `hystrix.command.default.execution.isolation.thread.timeoutInMilliseconds: 20000` — 20 s timeout · config/src/main/resources/shared/gateway.yml:L7
- gateway · `ribbon.ReadTimeout: 20000` — 20 s timeout · config/src/main/resources/shared/gateway.yml:L10
- gateway · `ribbon.ConnectTimeout: 20000` — 20 s timeout · config/src/main/resources/shared/gateway.yml:L11
- gateway · `zuul.host.connect-timeout-millis: 20000` — 20 s timeout · config/src/main/resources/shared/gateway.yml:L16
- gateway · `zuul.host.socket-timeout-millis: 20000` — 20 s timeout · config/src/main/resources/shared/gateway.yml:L17
- notification-service · `spring.data.mongodb.host: notification-mongodb` — the service's own store · config/src/main/resources/shared/notification-service.yml:L31
- statistics-service · `spring.data.mongodb.host: statistics-mongodb` — the service's own store · config/src/main/resources/shared/statistics-service.yml:L13
- gateway · `spring.cloud.config.fail-fast: true` — fails fast when the config server is unreachable · fail-fast on · gateway/src/main/resources/bootstrap.yml:L7
- monitoring · `spring.cloud.config.fail-fast: true` — fails fast when the config server is unreachable · fail-fast on · monitoring/src/main/resources/bootstrap.yml:L7
- notification-service · `spring.cloud.config.fail-fast: true` — fails fast when the config server is unreachable · fail-fast on · notification-service/src/main/resources/bootstrap.yml:L7
- registry · `spring.cloud.config.fail-fast: true` — fails fast when the config server is unreachable · fail-fast on · registry/src/main/resources/bootstrap.yml:L7
- statistics-service · `spring.cloud.config.fail-fast: true` — fails fast when the config server is unreachable · fail-fast on · statistics-service/src/main/resources/bootstrap.yml:L7
- turbine-stream-service · `spring.cloud.config.fail-fast: true` — fails fast when the config server is unreachable · fail-fast on · turbine-stream-service/src/main/resources/bootstrap.yml:L7
- one store per service — 4 modules carry 4 distinct datasource hosts: account-service → account-mongodb, auth-service → auth-mongodb, notification-service → notification-mongodb, statistics-service → statistics-mongodb · config/src/main/resources/shared/account-service.yml:L13 · config/src/main/resources/shared/auth-service.yml:L4 · config/src/main/resources/shared/notification-service.yml:L31 · config/src/main/resources/shared/statistics-service.yml:L13

### Impacted assets

- sandeep-chanda/piggymetricsJava · config/src/main/resources/application.yml
- sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/registry.yml
- sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml
- sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java
- sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/config/OAuth2AuthorizationConfig.java
- sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/domain/User.java
- sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/repository/UserRepository.java
- sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/auth-service.yml
- sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java
- sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/domain/Account.java
- sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/repository/AccountRepository.java
- sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/client/AuthServiceClient.java
- sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/client/StatisticsServiceClient.java
- sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/account-service.yml
- sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java
- sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/domain/Account.java
- sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/domain/timeseries/DataPoint.java
- sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/repository/DataPointRepository.java
- sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/client/ExchangeRatesClient.java
- sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/statistics-service.yml
- sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java
- sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/domain/Recipient.java
- sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/repository/RecipientRepository.java
- sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java
- sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/service/NotificationServiceImpl.java
- sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/notification-service.yml
- sandeep-chanda/piggymetricsJava · account-service/pom.xml
- sandeep-chanda/piggymetricsJava · auth-service/pom.xml
- sandeep-chanda/piggymetricsJava · config/pom.xml
- sandeep-chanda/piggymetricsJava · gateway/pom.xml
- sandeep-chanda/piggymetricsJava · monitoring/pom.xml
- sandeep-chanda/piggymetricsJava · notification-service/pom.xml
- {'repo_slug': 'sandeep-chanda/piggymetricsJava'}
- sandeep-chanda/piggymetricsJava · registry/pom.xml
- sandeep-chanda/piggymetricsJava · statistics-service/pom.xml
- sandeep-chanda/piggymetricsJava · turbine-stream-service/pom.xml

### Code highlights

- account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L12 · @EnableDiscoveryClient
- account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L13 · @EnableOAuth2Client
- account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L14 · @EnableFeignClients
- account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L15 · @EnableCircuitBreaker
- account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L16 · @EnableGlobalMethodSecurity
- auth-service/src/main/java/com/piggymetrics/auth/AuthApplication.java:L10 · @EnableResourceServer
- auth-service/src/main/java/com/piggymetrics/auth/AuthApplication.java:L11 · @EnableDiscoveryClient
- auth-service/src/main/java/com/piggymetrics/auth/AuthApplication.java:L12 · @EnableGlobalMethodSecurity
- config/src/main/java/com/piggymetrics/config/ConfigApplication.java:L8 · @EnableConfigServer
- gateway/src/main/java/com/piggymetrics/gateway/GatewayApplication.java:L9 · @EnableDiscoveryClient
- gateway/src/main/java/com/piggymetrics/gateway/GatewayApplication.java:L10 · @EnableZuulProxy
- monitoring/src/main/java/com/piggymetrics/monitoring/MonitoringApplication.java:L8 · @EnableHystrixDashboard
- notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L19 · @EnableDiscoveryClient
- notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L20 · @EnableOAuth2Client
- notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L21 · @EnableFeignClients
- notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L22 · @EnableGlobalMethodSecurity
- notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L23 · @EnableScheduling
- registry/src/main/java/com/piggymetrics/registry/RegistryApplication.java:L8 · @EnableEurekaServer
- statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L22 · @EnableDiscoveryClient
- statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L23 · @EnableOAuth2Client
- statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L24 · @EnableFeignClients
- statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L25 · @EnableGlobalMethodSecurity
- turbine-stream-service/src/main/java/com/piggymetrics/turbine/TurbineStreamServiceApplication.java:L9 · @EnableTurbineStream
- turbine-stream-service/src/main/java/com/piggymetrics/turbine/TurbineStreamServiceApplication.java:L10 · @EnableDiscoveryClient
- account-service/src/main/java/com/piggymetrics/account/client/AuthServiceClient.java:L9 · @FeignClient(name = "auth-service")
- account-service/src/main/java/com/piggymetrics/account/client/StatisticsServiceClient.java:L10 · @FeignClient(name = "statistics-service")
- notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java:L9 · @FeignClient(name = "account-service")
- statistics-service/src/main/java/com/piggymetrics/statistics/client/ExchangeRatesClient.java:L10 · @FeignClient(url = "${rates.url}", name = "rates-client")
- account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L20 · @RequestMapping(path = "/{name}", method = RequestMethod.GET)
- account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L25 · @RequestMapping(path = "/current", method = RequestMethod.GET)
- account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L30 · @RequestMapping(path = "/current", method = RequestMethod.PUT)
- account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L35 · @RequestMapping(path = "/", method = RequestMethod.POST)
- auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L16 · @RequestMapping("/users")
- auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L22 · @RequestMapping(value = "/current", method = RequestMethod.GET)
- auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L28 · @RequestMapping(method = RequestMethod.POST)
- notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L15 · @RequestMapping("/recipients")
- notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L21 · @RequestMapping(path = "/current", method = RequestMethod.GET)
- notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L26 · @RequestMapping(path = "/current", method = RequestMethod.PUT)
- statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L20 · @RequestMapping(value = "/current", method = RequestMethod.GET)
- statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L26 · @RequestMapping(value = "/{accountName}", method = RequestMethod.GET)
- statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L32 · @RequestMapping(value = "/{accountName}", method = RequestMethod.PUT)
- config/src/main/resources/shared/gateway.yml:L20 · zuul.routes.auth-service · /uaa/**
- config/src/main/resources/shared/gateway.yml:L26 · zuul.routes.account-service · /accounts/**
- config/src/main/resources/shared/gateway.yml:L32 · zuul.routes.statistics-service · /statistics/**
- config/src/main/resources/shared/gateway.yml:L38 · zuul.routes.notification-service · /notifications/**

## Target shape
- PiggyMetricsDotNet · .NET · proposed; nothing here is a cited path
- runtime · .NET 8 LTS · ASP.NET Core · one project per runtime service plus Shared · default · instead of: one monolith project · a worker per service without ASP.NET Core · so: each service deploys and scales alone, as the source's do; Shared is the only coupling
- gateway · YARP · the same route prefixes as static clusters · static UI served from the gateway host · default · instead of: Ocelot · an edge proxy outside the solution (nginx, Envoy) · so: the route table lives in the gateway project's configuration; the static UI moves host only
- discovery · none · service addresses are configuration · default · instead of: Consul · Kubernetes DNS · Steeltoe discovery · so: every callee address is a setting; adding an instance changes configuration, not a registry
- configuration · appsettings.json per service + environment variables for secrets · the config server is retired · default · instead of: a config service (Azure App Configuration, Consul KV) · keep the config server · so: the config module is retired; each service owns its settings file and reads secrets from the environment
- calls · typed HttpClient per edge · Polly timeout matching the source · the same failure behaviour per edge · default · instead of: Refit typed clients · gRPC (changes the contract) · a service mesh for timeouts · so: each edge keeps the source's timeout and failure behaviour in code the service owns
- persistence · MongoDB.Driver on the same databases and collections · same BSON field names and _id semantics · default · instead of: EF Core with a Mongo provider · a relational store (changes the data map) · so: the destination reads and writes the source's collections in place; no data migration and no rollback of data
- auth · OpenIddict issuing the same grants and scopes · a user-info endpoint in the shape the resource servers read · default · instead of: Duende IdentityServer (commercial licence) · an external identity provider (changes the token endpoint) · so: tokens keep the source's grants and scopes; resource servers validate the way they do today
- resilience · Polly · ASP.NET health checks · OpenTelemetry · dashboard and metrics stream retired · default · instead of: Steeltoe · keep the dashboard and metrics stream on the source until cutover · so: the dashboard and metrics stream are retired; health and telemetry come from the platform
- scheduling · hosted BackgroundService per cron · MailKit over the same SMTP settings · default · instead of: Quartz.NET · Hangfire · a platform cron calling an endpoint · so: each cron runs inside its service as a hosted service; no scheduler store to migrate
- layout · Shared · gateway → Gateway · auth-service → AuthService · account-service → AccountService · statistics-service → StatisticsService · notification-service → NotificationService
- config (config-server) · retire · configuration per service · default
- monitoring (dashboard) · retire · health checks and metrics · default
- registry (registry) · retire · addresses are configuration · default
- turbine-stream-service (metrics-stream) · retire · health checks and metrics · default

## Contract mapping
- C1 · GET /uaa/users/current → AuthService · UserController.GetUser · policy user · check: GET with a user token → the source's body; without one → 401 · proposed
- C2 · POST /uaa/users → AuthService · UserController.CreateUser · policy server · check: POST with a server token → 200; with a user token → 403 · proposed
- C3 · POST /uaa/oauth/token → AuthService · token endpoint · policy client credentials · check: each grant issues a token the resource servers accept · proposed
- C4 · GET /accounts/{name} → AccountService · AccountController.GetAccountByName · policy server, or name == demo · check: GET with a server token → 200; with a user token → 403; the named exception without a token → 200 · proposed
- C5 · GET /accounts/current → AccountService · AccountController.GetCurrentAccount · policy user · check: GET with a user token → the source's body; without one → 401 · proposed
- C6 · PUT /accounts/current → AccountService · AccountController.SaveCurrentAccount · policy user · check: PUT with a user token → the source's body; without one → 401 · proposed
- C7 · POST /accounts → AccountService · AccountController.CreateNewAccount · policy anonymous · check: POST without a token → the source's status and body · proposed
- C8 · GET /statistics/current → StatisticsService · StatisticsController.GetCurrentAccountStatistics · policy user · check: GET with a user token → the source's body; without one → 401 · proposed
- C9 · GET /statistics/{accountName} → StatisticsService · StatisticsController.GetStatisticsByAccountName · policy server, or accountName == demo · check: GET with a server token → 200; with a user token → 403; the named exception without a token → 200 · proposed
- C10 · PUT /statistics/{accountName} → StatisticsService · StatisticsController.SaveAccountStatistics · policy server · check: PUT with a server token → 200; with a user token → 403 · proposed
- C11 · GET /notifications/recipients/current → NotificationService · RecipientController.GetCurrentNotificationsSettings · policy user · check: GET with a user token → the source's body; without one → 401 · proposed
- C12 · PUT /notifications/recipients/current → NotificationService · RecipientController.SaveCurrentNotificationsSettings · policy user · check: PUT with a user token → the source's body; without one → 401 · proposed

## Data mapping
- users · User → AuthService · User · _id = username · same collection and field names · proposed
- accounts · Account → AccountService · Account · _id = name · embedded Item, Saving · same collection and field names · proposed
- accounts · Account → StatisticsService · Account (request shape) · embedded Item, Saving · same collection and field names · proposed
- datapoints · DataPoint → StatisticsService · DataPoint · _id = id · embedded DataPointId, ItemMetric · same collection and field names · proposed
- recipients · Recipient → NotificationService · Recipient · _id = accountName · embedded NotificationSettings · same collection and field names · proposed

## Scope
### Scope

- In: gateway, auth-service, account-service, statistics-service, notification-service · every endpoint (12), stored collection (4), outbound client (4) and scheduled job (2) the read states
- Platform, not units: config (config-server), monitoring (dashboard), registry (registry), turbine-stream-service (metrics-stream) · each carries a decision in the Target shape
- Out: PiggyMetricsDotNet as evidence · the destination is written as proposals and never cited

### Acceptance criteria

- every project builds and Shared's bearer handler validates a source token
- each service's plan is done: its contract rows replay with the source's status and body
- every contract row holds parity through both gateways
- every route points at the destination and the source runs shadow only
- each retired module is off and nothing reads it

## Plans
### Plans

### P1 · Solution skeleton and Shared
the .NET solution every service plan builds on
- Target: Shared · .NET
- Done when: every project builds; Shared's bearer handler accepts a token the source's authorization server issued and refuses one it did not

1. Solution and one project per runtime service (Gateway, AuthService, AccountService, StatisticsService, NotificationService) plus Shared · .NET · from account-service/pom.xml
2. Shared · bearer handler that validates a token the way the resource servers do · calls http://auth-service:5000/uaa/users/current · account-service/src/main/java/com/piggymetrics/account/config/ResourceServerConfig.java
3. Shared · store conventions and serializers for the documents' field names and ids · MongoDB.Driver on the same databases and collections · same BSON field names and _id semantics · account-service/src/main/java/com/piggymetrics/account/repository/AccountRepository.java
4. Shared · typed-client base with the source's timeout and a client-credentials token cache · typed HttpClient per edge · Polly timeout matching the source · the same failure behaviour per edge
5. Shared · health checks and JSON options (enums as names, unknown fields ignored, the source's date format)

### P2 · AuthService · auth-service
3 endpoints · 1 collection · 0 clients · 0 jobs · 9 tests
- Target: AuthService · .NET · port 5000 · path /uaa · store auth-mongodb
- Depends on: P1
- Rows: Contract C1, C2, C3 · Data users
- Done when: C1–C3 replay through AuthService with the source's status and body (Contract map); the 5 ported test classes (9 tests) are green

1. Port User (users) → AuthService · User · _id = username · fields username, password · same collection and field names · auth-service/src/main/java/com/piggymetrics/auth/domain/User.java:L10
2. Port UserRepository on User → AuthService · UserRepository on auth-mongodb · auth-service/src/main/java/com/piggymetrics/auth/repository/UserRepository.java
3. C1 · GET /uaa/users/current → AuthService · UserController.GetUser · policy user · returns Principal · check: GET with a user token → the source's body; without one → 401 · auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L22
4. C2 · POST /uaa/users → AuthService · UserController.CreateUser · policy server · body User (validated) · returns void · check: POST with a server token → 200; with a user token → 403 · auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L28
5. C3 · POST /uaa/oauth/token → AuthService · token endpoint · policy client credentials · body grant_type=client_credentials | password | refresh_token · returns token · check: each grant issues a token the resource servers accept · auth-service/src/main/java/com/piggymetrics/auth/config/OAuth2AuthorizationConfig.java
6. Authorization server with the 4 clients and their grants and scopes → AuthService · OpenIddict issuing the same grants and scopes · a user-info endpoint in the shape the resource servers read · grants client_credentials, password, refresh_token · auth-service/src/main/java/com/piggymetrics/auth/config/OAuth2AuthorizationConfig.java
7. Settings → AuthService · appsettings.json per service + environment variables for secrets · the config server is retired · port 5000 · path /uaa · store auth-mongodb/piggymetrics · keys from auth-service/src/main/resources/bootstrap.yml, config/src/main/resources/shared/auth-service.yml
8. Port test AuthServiceApplicationTests · same names and assertions · AuthService.Tests · auth-service/src/test/java/com/piggymetrics/auth/AuthServiceApplicationTests.java
9. Port test UserControllerTest · same names and assertions · AuthService.Tests · auth-service/src/test/java/com/piggymetrics/auth/controller/UserControllerTest.java
10. Port test UserRepositoryTest · same names and assertions · AuthService.Tests · auth-service/src/test/java/com/piggymetrics/auth/repository/UserRepositoryTest.java
11. Port test UserServiceTest · same names and assertions · AuthService.Tests · auth-service/src/test/java/com/piggymetrics/auth/service/UserServiceTest.java
12. Port test MongoUserDetailsServiceTest · same names and assertions · AuthService.Tests · auth-service/src/test/java/com/piggymetrics/auth/service/security/MongoUserDetailsServiceTest.java

### P3 · StatisticsService · statistics-service
3 endpoints · 1 collection · 1 client · 0 jobs · 16 tests
- Target: StatisticsService · .NET · port 7000 · path /statistics · store statistics-mongodb
- Depends on: P1
- Rows: Contract C8, C9, C10 · Data datapoints · Edges E3
- Done when: C8–C10 replay through StatisticsService with the source's status and body (Contract map); E3 behaves as the source when the callee is down; the 6 ported test classes (16 tests) are green

1. Port Account (accounts · shape) → StatisticsService · Account (request shape) · fields incomes, expenses, saving · embedded Item, Saving · same field names · statistics-service/src/main/java/com/piggymetrics/statistics/domain/Account.java:L10
2. Port DataPoint (datapoints) → StatisticsService · DataPoint · _id = id · fields id, incomes, expenses, statistics, rates · embedded DataPointId, ItemMetric · same collection and field names · statistics-service/src/main/java/com/piggymetrics/statistics/domain/timeseries/DataPoint.java:L15
3. Port DataPointRepository on DataPoint → StatisticsService · DataPointRepository on statistics-mongodb · statistics-service/src/main/java/com/piggymetrics/statistics/repository/DataPointRepository.java
4. C8 · GET /statistics/current → StatisticsService · StatisticsController.GetCurrentAccountStatistics · policy user · returns List<DataPoint> · check: GET with a user token → the source's body; without one → 401 · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L20
5. C9 · GET /statistics/{accountName} → StatisticsService · StatisticsController.GetStatisticsByAccountName · policy server, or accountName == demo · returns List<DataPoint> · check: GET with a server token → 200; with a user token → 403; the named exception without a token → 200 · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L26
6. C10 · PUT /statistics/{accountName} → StatisticsService · StatisticsController.SaveAccountStatistics · policy server · body Account (validated) · returns void · check: PUT with a server token → 200; with a user token → 403 · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L32
7. Client for GET /latest on rates-client → StatisticsService typed client · E3 · on failure: fallback · ExchangeRatesClientFallback · the source's timeout · statistics-service/src/main/java/com/piggymetrics/statistics/client/ExchangeRatesClient.java
8. Settings → StatisticsService · appsettings.json per service + environment variables for secrets · the config server is retired · port 7000 · path /statistics · store statistics-mongodb/piggymetrics · keys from config/src/main/resources/shared/statistics-service.yml, statistics-service/src/main/resources/bootstrap.yml
9. Port test StatisticsServiceApplicationTests · same names and assertions · StatisticsService.Tests · statistics-service/src/test/java/com/piggymetrics/statistics/StatisticsServiceApplicationTests.java
10. Port test ExchangeRatesClientTest · same names and assertions · StatisticsService.Tests · statistics-service/src/test/java/com/piggymetrics/statistics/client/ExchangeRatesClientTest.java
11. Port test StatisticsControllerTest · same names and assertions · StatisticsService.Tests · statistics-service/src/test/java/com/piggymetrics/statistics/controller/StatisticsControllerTest.java
12. Port test DataPointRepositoryTest · same names and assertions · StatisticsService.Tests · statistics-service/src/test/java/com/piggymetrics/statistics/repository/DataPointRepositoryTest.java
13. Port test ExchangeRatesServiceImplTest · same names and assertions · StatisticsService.Tests · statistics-service/src/test/java/com/piggymetrics/statistics/service/ExchangeRatesServiceImplTest.java
14. Port test StatisticsServiceImplTest · same names and assertions · StatisticsService.Tests · statistics-service/src/test/java/com/piggymetrics/statistics/service/StatisticsServiceImplTest.java

### P4 · AccountService · account-service
4 endpoints · 1 collection · 2 clients · 0 jobs · 14 tests
- Target: AccountService · .NET · port 6000 · path /accounts · store account-mongodb
- Depends on: P1, P2 (E1), P3 (E2)
- Rows: Contract C4, C5, C6, C7 · Data accounts · Edges E1, E2
- Done when: C4–C7 replay through AccountService with the source's status and body (Contract map); E1, E2 behave as the source when the callee is down; the 5 ported test classes (14 tests) are green

1. Port Account (accounts) → AccountService · Account · _id = name · fields name, lastSeen, incomes, expenses, saving, note · embedded Item, Saving · same collection and field names · account-service/src/main/java/com/piggymetrics/account/domain/Account.java:L13
2. Port AccountRepository on Account → AccountService · AccountRepository on account-mongodb · account-service/src/main/java/com/piggymetrics/account/repository/AccountRepository.java
3. C4 · GET /accounts/{name} → AccountService · AccountController.GetAccountByName · policy server, or name == demo · returns Account · check: GET with a server token → 200; with a user token → 403; the named exception without a token → 200 · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L20
4. C5 · GET /accounts/current → AccountService · AccountController.GetCurrentAccount · policy user · returns Account · check: GET with a user token → the source's body; without one → 401 · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L25
5. C6 · PUT /accounts/current → AccountService · AccountController.SaveCurrentAccount · policy user · body Account (validated) · returns void · check: PUT with a user token → the source's body; without one → 401 · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L30
6. C7 · POST /accounts → AccountService · AccountController.CreateNewAccount · policy anonymous · body User (validated) · returns Account · check: POST without a token → the source's status and body · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L35
7. Client for POST /uaa/users on auth-service → AccountService typed client · E1 · on failure: propagates · the source's timeout · account-service/src/main/java/com/piggymetrics/account/client/AuthServiceClient.java
8. Client for PUT /statistics/{accountName} on statistics-service → AccountService typed client · E2 · on failure: fallback · StatisticsServiceClientFallback · the source's timeout · account-service/src/main/java/com/piggymetrics/account/client/StatisticsServiceClient.java
9. Policies from the matchers → AccountService authorization policies · account-service/src/main/java/com/piggymetrics/account/config/ResourceServerConfig.java
10. Settings → AccountService · appsettings.json per service + environment variables for secrets · the config server is retired · port 6000 · path /accounts · store account-mongodb/piggymetrics · keys from account-service/src/main/resources/bootstrap.yml, config/src/main/resources/shared/account-service.yml
11. Port test AccountServiceApplicationTests · same names and assertions · AccountService.Tests · account-service/src/test/java/com/piggymetrics/account/AccountServiceApplicationTests.java
12. Port test StatisticsServiceClientFallbackTest · same names and assertions · AccountService.Tests · account-service/src/test/java/com/piggymetrics/account/client/StatisticsServiceClientFallbackTest.java
13. Port test AccountControllerTest · same names and assertions · AccountService.Tests · account-service/src/test/java/com/piggymetrics/account/controller/AccountControllerTest.java
14. Port test AccountRepositoryTest · same names and assertions · AccountService.Tests · account-service/src/test/java/com/piggymetrics/account/repository/AccountRepositoryTest.java
15. Port test AccountServiceTest · same names and assertions · AccountService.Tests · account-service/src/test/java/com/piggymetrics/account/service/AccountServiceTest.java

### P5 · NotificationService · notification-service
2 endpoints · 1 collection · 1 client · 2 jobs · 18 tests
- Target: NotificationService · .NET · port 8000 · path /notifications · store notification-mongodb
- Depends on: P1, P4 (E4)
- Rows: Contract C11, C12 · Data recipients · Edges E4
- Done when: C11, C12 replay through NotificationService with the source's status and body (Contract map); E4 behaves as the source when the callee is down; 2 schedules fire on the source's cron; the 6 ported test classes (18 tests) are green

1. Port Recipient (recipients) → NotificationService · Recipient · _id = accountName · fields accountName, email, scheduledNotifications · embedded NotificationSettings · same collection and field names · notification-service/src/main/java/com/piggymetrics/notification/domain/Recipient.java:L11
2. Port RecipientRepository on Recipient → NotificationService · RecipientRepository on notification-mongodb · notification-service/src/main/java/com/piggymetrics/notification/repository/RecipientRepository.java
3. C11 · GET /notifications/recipients/current → NotificationService · RecipientController.GetCurrentNotificationsSettings · policy user · returns Object · check: GET with a user token → the source's body; without one → 401 · notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L21
4. C12 · PUT /notifications/recipients/current → NotificationService · RecipientController.SaveCurrentNotificationsSettings · policy user · body Recipient (validated) · returns Object · check: PUT with a user token → the source's body; without one → 401 · notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L26
5. Client for GET /accounts/{accountName} on account-service → NotificationService typed client · E4 · on failure: caught and logged in NotificationServiceImpl · the source's timeout · notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java
6. Job NotificationServiceImpl.sendBackupNotifications on cron 0 0 12 * * * → NotificationService hosted schedule on the same cron · one failure does not stop the others · notification-service/src/main/java/com/piggymetrics/notification/service/NotificationServiceImpl.java
7. Job NotificationServiceImpl.sendRemindNotifications on cron 0 0 0 * * * → NotificationService hosted schedule on the same cron · one failure does not stop the others · notification-service/src/main/java/com/piggymetrics/notification/service/NotificationServiceImpl.java
8. Settings → NotificationService · appsettings.json per service + environment variables for secrets · the config server is retired · port 8000 · path /notifications · store notification-mongodb/piggymetrics · keys from config/src/main/resources/shared/notification-service.yml, notification-service/src/main/resources/bootstrap.yml
9. Port test NotificationServiceApplicationTests · same names and assertions · NotificationService.Tests · notification-service/src/test/java/com/piggymetrics/notification/NotificationServiceApplicationTests.java
10. Port test RecipientControllerTest · same names and assertions · NotificationService.Tests · notification-service/src/test/java/com/piggymetrics/notification/controller/RecipientControllerTest.java
11. Port test RecipientRepositoryTest · same names and assertions · NotificationService.Tests · notification-service/src/test/java/com/piggymetrics/notification/repository/RecipientRepositoryTest.java
12. Port test EmailServiceImplTest · same names and assertions · NotificationService.Tests · notification-service/src/test/java/com/piggymetrics/notification/service/EmailServiceImplTest.java
13. Port test NotificationServiceImplTest · same names and assertions · NotificationService.Tests · notification-service/src/test/java/com/piggymetrics/notification/service/NotificationServiceImplTest.java
14. Port test RecipientServiceImplTest · same names and assertions · NotificationService.Tests · notification-service/src/test/java/com/piggymetrics/notification/service/RecipientServiceImplTest.java

### P6 · Gateway · gateway
0 endpoints · 0 collections · 0 clients · 0 jobs · 2 tests
- Target: Gateway · .NET · port 4000
- Depends on: P1, P2, P3, P4, P5
- Done when: each of 4 prefixes (/uaa/**, /accounts/**, /statistics/**, /notifications/**) reaches its destination service and an unlisted path is 404; the static UI is served unchanged

1. Route /uaa/** → http://auth-service:5000 · no prefix stripping → Gateway route to http://auth-service:5000 · config/src/main/resources/shared/gateway.yml:L21
2. Route /accounts/** → account-service · no prefix stripping → Gateway route to AccountService · config/src/main/resources/shared/gateway.yml:L27
3. Route /statistics/** → statistics-service · no prefix stripping → Gateway route to StatisticsService · config/src/main/resources/shared/gateway.yml:L33
4. Route /notifications/** → notification-service · no prefix stripping → Gateway route to NotificationService · config/src/main/resources/shared/gateway.yml:L39
5. Serve the static UI unchanged from the Gateway host · gateway/src/main/resources/static/
6. Timeouts and port 4000 from · config/src/main/resources/shared/gateway.yml
7. Timeouts and port 4000 from · gateway/src/main/resources/bootstrap.yml
8. Port test GatewayApplicationTests · same names and assertions · Gateway.Tests · gateway/src/test/java/com/piggymetrics/gateway/GatewayApplicationTests.java

### P7 · Parity harness
the contract replay through both gateways
- Depends on: P2, P3, P4, P5, P6
- Done when: C1–C12 hold parity: same status and body on the fields the callers read

1. Replay C1–C12 against source and destination with the same seed data; compare status and body on the fields the callers read
2. Stub the external clients identically on both sides
3. Auth matrix: each row with no token, a user token and a server token gives the status its policy says
4. Report parity per route

### P8 · Cutover runbook
route by route at the source gateway; the source runs shadow
- Depends on: P7
- Done when: every route points at the destination and the source runs shadow only

1. Switch /accounts/**, then /statistics/**, then /notifications/**, then /uaa/** at the source gateway; the auth route last
2. Shadow until each route's parity holds; roll a route back by pointing it at the source unit
3. Data needs no rollback: both ends read and write the same stores

### P9 · Retirement list
what is not carried and why
- Depends on: P8
- Done when: each retired module is off and nothing reads it

1. config (config-server) · retire · configuration per service
2. monitoring (dashboard) · retire · health checks and metrics
3. registry (registry) · retire · addresses are configuration
4. turbine-stream-service (metrics-stream) · retire · health checks and metrics
5. Broker and metrics stream starters · not carried

### Phases

- Phase 1 · Skeleton
  - Do: P1 · Solution skeleton and Shared
  - Done when: every project builds and Shared's bearer handler validates a source token
- Phase 2 · Services
  - Do: P2 · AuthService · auth-service
  - Do: P3 · StatisticsService · statistics-service
  - Do: P4 · AccountService · account-service
  - Do: P5 · NotificationService · notification-service
  - Do: P6 · Gateway · gateway
  - Done when: each service's plan is done: its contract rows replay with the source's status and body
- Phase 3 · Parity
  - Do: P7 · Parity harness
  - Done when: every contract row holds parity through both gateways
- Phase 4 · Cutover
  - Do: P8 · Cutover runbook
  - Done when: every route points at the destination and the source runs shadow only
- Phase 5 · Retirement
  - Do: P9 · Retirement list
  - Done when: each retired module is off and nothing reads it

### Change plan

- account-service/pom.xml:L1 · shared — P1 · Solution and one project per runtime service (Gateway, AuthService, AccountService, StatisticsService, NotificationService) plus Shared · .NET · from
- account-service/src/main/java/com/piggymetrics/account/config/ResourceServerConfig.java:L1 · shared — P1 · Shared · bearer handler that validates a token the way the resource servers do · calls http://auth-service:5000/uaa/users/current
- account-service/src/main/java/com/piggymetrics/account/repository/AccountRepository.java:L1 · shared — P1 · Shared · store conventions and serializers for the documents' field names and ids · MongoDB.Driver on the same databases and collections · same BSON field names and _id semantics
- auth-service/src/main/java/com/piggymetrics/auth/domain/User.java:L10 · auth-service — P2 · Port User (users) → AuthService · User · _id = username · fields username, password · same collection and field names
- auth-service/src/main/java/com/piggymetrics/auth/repository/UserRepository.java:L1 · auth-service — P2 · Port UserRepository on User → AuthService · UserRepository on auth-mongodb
- auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L22 · auth-service — P2 · C1 · GET /uaa/users/current → AuthService · UserController.GetUser · policy user · returns Principal · check: GET with a user token → the source's body; without one → 401
- auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L28 · auth-service — P2 · C2 · POST /uaa/users → AuthService · UserController.CreateUser · policy server · body User (validated) · returns void · check: POST with a server token → 200; with a user token → 403
- auth-service/src/main/java/com/piggymetrics/auth/config/OAuth2AuthorizationConfig.java:L1 · auth-service — P2 · C3 · POST /uaa/oauth/token → AuthService · token endpoint · policy client credentials · body grant_type=client_credentials | password | refresh_token · returns token · check: each grant issues a token the resource servers accept
- auth-service/src/main/java/com/piggymetrics/auth/config/OAuth2AuthorizationConfig.java:L1 · auth-service — P2 · Authorization server with the 4 clients and their grants and scopes → AuthService · OpenIddict issuing the same grants and scopes · a user-info endpoint in the shape the resource servers read · grants client_credentials, password, refresh_token
- config/src/main/resources/shared/auth-service.yml:L1 · auth-service — P2 · Settings → AuthService · appsettings.json per service + environment variables for secrets · the config server is retired · port 5000 · path /uaa · store auth-mongodb/piggymetrics · keys from auth-service/src/main/resources/bootstrap.yml,
- auth-service/src/test/java/com/piggymetrics/auth/AuthServiceApplicationTests.java:L1 · auth-service — P2 · Port test AuthServiceApplicationTests · same names and assertions · AuthService.Tests
- auth-service/src/test/java/com/piggymetrics/auth/controller/UserControllerTest.java:L1 · auth-service — P2 · Port test UserControllerTest · same names and assertions · AuthService.Tests
- auth-service/src/test/java/com/piggymetrics/auth/repository/UserRepositoryTest.java:L1 · auth-service — P2 · Port test UserRepositoryTest · same names and assertions · AuthService.Tests
- auth-service/src/test/java/com/piggymetrics/auth/service/UserServiceTest.java:L1 · auth-service — P2 · Port test UserServiceTest · same names and assertions · AuthService.Tests
- auth-service/src/test/java/com/piggymetrics/auth/service/security/MongoUserDetailsServiceTest.java:L1 · auth-service — P2 · Port test MongoUserDetailsServiceTest · same names and assertions · AuthService.Tests
- statistics-service/src/main/java/com/piggymetrics/statistics/domain/Account.java:L10 · statistics-service — P3 · Port Account (accounts · shape) → StatisticsService · Account (request shape) · fields incomes, expenses, saving · embedded Item, Saving · same field names
- statistics-service/src/main/java/com/piggymetrics/statistics/domain/timeseries/DataPoint.java:L15 · statistics-service — P3 · Port DataPoint (datapoints) → StatisticsService · DataPoint · _id = id · fields id, incomes, expenses, statistics, rates · embedded DataPointId, ItemMetric · same collection and field names
- statistics-service/src/main/java/com/piggymetrics/statistics/repository/DataPointRepository.java:L1 · statistics-service — P3 · Port DataPointRepository on DataPoint → StatisticsService · DataPointRepository on statistics-mongodb
- statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L20 · statistics-service — P3 · C8 · GET /statistics/current → StatisticsService · StatisticsController.GetCurrentAccountStatistics · policy user · returns List<DataPoint> · check: GET with a user token → the source's body; without one → 401
- statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L26 · statistics-service — P3 · C9 · GET /statistics/{accountName} → StatisticsService · StatisticsController.GetStatisticsByAccountName · policy server, or accountName == demo · returns List<DataPoint> · check: GET with a server token → 200; with a user token → 403; the named exception without a token → 200
- statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L32 · statistics-service — P3 · C10 · PUT /statistics/{accountName} → StatisticsService · StatisticsController.SaveAccountStatistics · policy server · body Account (validated) · returns void · check: PUT with a server token → 200; with a user token → 403
- statistics-service/src/main/java/com/piggymetrics/statistics/client/ExchangeRatesClient.java:L1 · statistics-service — P3 · Client for GET /latest on rates-client → StatisticsService typed client · E3 · on failure: fallback · ExchangeRatesClientFallback · the source's timeout
- statistics-service/src/main/resources/bootstrap.yml:L1 · statistics-service — P3 · Settings → StatisticsService · appsettings.json per service + environment variables for secrets · the config server is retired · port 7000 · path /statistics · store statistics-mongodb/piggymetrics · keys from config/src/main/resources/shared/statistics-service.yml,
- statistics-service/src/test/java/com/piggymetrics/statistics/StatisticsServiceApplicationTests.java:L1 · statistics-service — P3 · Port test StatisticsServiceApplicationTests · same names and assertions · StatisticsService.Tests
- statistics-service/src/test/java/com/piggymetrics/statistics/client/ExchangeRatesClientTest.java:L1 · statistics-service — P3 · Port test ExchangeRatesClientTest · same names and assertions · StatisticsService.Tests
- statistics-service/src/test/java/com/piggymetrics/statistics/controller/StatisticsControllerTest.java:L1 · statistics-service — P3 · Port test StatisticsControllerTest · same names and assertions · StatisticsService.Tests
- statistics-service/src/test/java/com/piggymetrics/statistics/repository/DataPointRepositoryTest.java:L1 · statistics-service — P3 · Port test DataPointRepositoryTest · same names and assertions · StatisticsService.Tests
- statistics-service/src/test/java/com/piggymetrics/statistics/service/ExchangeRatesServiceImplTest.java:L1 · statistics-service — P3 · Port test ExchangeRatesServiceImplTest · same names and assertions · StatisticsService.Tests
- statistics-service/src/test/java/com/piggymetrics/statistics/service/StatisticsServiceImplTest.java:L1 · statistics-service — P3 · Port test StatisticsServiceImplTest · same names and assertions · StatisticsService.Tests
- account-service/src/main/java/com/piggymetrics/account/domain/Account.java:L13 · account-service — P4 · Port Account (accounts) → AccountService · Account · _id = name · fields name, lastSeen, incomes, expenses, saving, note · embedded Item, Saving · same collection and field names
- account-service/src/main/java/com/piggymetrics/account/repository/AccountRepository.java:L1 · account-service — P4 · Port AccountRepository on Account → AccountService · AccountRepository on account-mongodb
- account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L20 · account-service — P4 · C4 · GET /accounts/{name} → AccountService · AccountController.GetAccountByName · policy server, or name == demo · returns Account · check: GET with a server token → 200; with a user token → 403; the named exception without a token → 200
- account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L25 · account-service — P4 · C5 · GET /accounts/current → AccountService · AccountController.GetCurrentAccount · policy user · returns Account · check: GET with a user token → the source's body; without one → 401
- account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L30 · account-service — P4 · C6 · PUT /accounts/current → AccountService · AccountController.SaveCurrentAccount · policy user · body Account (validated) · returns void · check: PUT with a user token → the source's body; without one → 401
- account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L35 · account-service — P4 · C7 · POST /accounts → AccountService · AccountController.CreateNewAccount · policy anonymous · body User (validated) · returns Account · check: POST without a token → the source's status and body
- account-service/src/main/java/com/piggymetrics/account/client/AuthServiceClient.java:L1 · account-service — P4 · Client for POST /uaa/users on auth-service → AccountService typed client · E1 · on failure: propagates · the source's timeout
- account-service/src/main/java/com/piggymetrics/account/client/StatisticsServiceClient.java:L1 · account-service — P4 · Client for PUT /statistics/{accountName} on statistics-service → AccountService typed client · E2 · on failure: fallback · StatisticsServiceClientFallback · the source's timeout
- account-service/src/main/java/com/piggymetrics/account/config/ResourceServerConfig.java:L1 · account-service — P4 · Policies from the matchers → AccountService authorization policies
- config/src/main/resources/shared/account-service.yml:L1 · account-service — P4 · Settings → AccountService · appsettings.json per service + environment variables for secrets · the config server is retired · port 6000 · path /accounts · store account-mongodb/piggymetrics · keys from account-service/src/main/resources/bootstrap.yml,
- account-service/src/test/java/com/piggymetrics/account/AccountServiceApplicationTests.java:L1 · account-service — P4 · Port test AccountServiceApplicationTests · same names and assertions · AccountService.Tests
- account-service/src/test/java/com/piggymetrics/account/client/StatisticsServiceClientFallbackTest.java:L1 · account-service — P4 · Port test StatisticsServiceClientFallbackTest · same names and assertions · AccountService.Tests
- account-service/src/test/java/com/piggymetrics/account/controller/AccountControllerTest.java:L1 · account-service — P4 · Port test AccountControllerTest · same names and assertions · AccountService.Tests
- account-service/src/test/java/com/piggymetrics/account/repository/AccountRepositoryTest.java:L1 · account-service — P4 · Port test AccountRepositoryTest · same names and assertions · AccountService.Tests
- account-service/src/test/java/com/piggymetrics/account/service/AccountServiceTest.java:L1 · account-service — P4 · Port test AccountServiceTest · same names and assertions · AccountService.Tests
- notification-service/src/main/java/com/piggymetrics/notification/domain/Recipient.java:L11 · notification-service — P5 · Port Recipient (recipients) → NotificationService · Recipient · _id = accountName · fields accountName, email, scheduledNotifications · embedded NotificationSettings · same collection and field names
- notification-service/src/main/java/com/piggymetrics/notification/repository/RecipientRepository.java:L1 · notification-service — P5 · Port RecipientRepository on Recipient → NotificationService · RecipientRepository on notification-mongodb
- notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L21 · notification-service — P5 · C11 · GET /notifications/recipients/current → NotificationService · RecipientController.GetCurrentNotificationsSettings · policy user · returns Object · check: GET with a user token → the source's body; without one → 401
- notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L26 · notification-service — P5 · C12 · PUT /notifications/recipients/current → NotificationService · RecipientController.SaveCurrentNotificationsSettings · policy user · body Recipient (validated) · returns Object · check: PUT with a user token → the source's body; without one → 401
- notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java:L1 · notification-service — P5 · Client for GET /accounts/{accountName} on account-service → NotificationService typed client · E4 · on failure: caught and logged in NotificationServiceImpl · the source's timeout
- notification-service/src/main/java/com/piggymetrics/notification/service/NotificationServiceImpl.java:L1 · notification-service — P5 · Job NotificationServiceImpl.sendBackupNotifications on cron 0 0 12 * * * → NotificationService hosted schedule on the same cron · one failure does not stop the others
- notification-service/src/main/java/com/piggymetrics/notification/service/NotificationServiceImpl.java:L1 · notification-service — P5 · Job NotificationServiceImpl.sendRemindNotifications on cron 0 0 0 * * * → NotificationService hosted schedule on the same cron · one failure does not stop the others
- notification-service/src/main/resources/bootstrap.yml:L1 · notification-service — P5 · Settings → NotificationService · appsettings.json per service + environment variables for secrets · the config server is retired · port 8000 · path /notifications · store notification-mongodb/piggymetrics · keys from config/src/main/resources/shared/notification-service.yml,
- notification-service/src/test/java/com/piggymetrics/notification/NotificationServiceApplicationTests.java:L1 · notification-service — P5 · Port test NotificationServiceApplicationTests · same names and assertions · NotificationService.Tests
- notification-service/src/test/java/com/piggymetrics/notification/controller/RecipientControllerTest.java:L1 · notification-service — P5 · Port test RecipientControllerTest · same names and assertions · NotificationService.Tests
- notification-service/src/test/java/com/piggymetrics/notification/repository/RecipientRepositoryTest.java:L1 · notification-service — P5 · Port test RecipientRepositoryTest · same names and assertions · NotificationService.Tests
- notification-service/src/test/java/com/piggymetrics/notification/service/EmailServiceImplTest.java:L1 · notification-service — P5 · Port test EmailServiceImplTest · same names and assertions · NotificationService.Tests
- notification-service/src/test/java/com/piggymetrics/notification/service/NotificationServiceImplTest.java:L1 · notification-service — P5 · Port test NotificationServiceImplTest · same names and assertions · NotificationService.Tests
- notification-service/src/test/java/com/piggymetrics/notification/service/RecipientServiceImplTest.java:L1 · notification-service — P5 · Port test RecipientServiceImplTest · same names and assertions · NotificationService.Tests
- config/src/main/resources/shared/gateway.yml:L21 · gateway — P6 · Route /uaa/** → http://auth-service:5000 · no prefix stripping → Gateway route to http://auth-service:5000
- config/src/main/resources/shared/gateway.yml:L27 · gateway — P6 · Route /accounts/** → account-service · no prefix stripping → Gateway route to AccountService
- config/src/main/resources/shared/gateway.yml:L33 · gateway — P6 · Route /statistics/** → statistics-service · no prefix stripping → Gateway route to StatisticsService
- config/src/main/resources/shared/gateway.yml:L39 · gateway — P6 · Route /notifications/** → notification-service · no prefix stripping → Gateway route to NotificationService
- config/src/main/resources/shared/gateway.yml:L1 · gateway — P6 · Timeouts and port 4000 from
- gateway/src/main/resources/bootstrap.yml:L1 · gateway — P6 · Timeouts and port 4000 from
- gateway/src/test/java/com/piggymetrics/gateway/GatewayApplicationTests.java:L1 · gateway — P6 · Port test GatewayApplicationTests · same names and assertions · Gateway.Tests

### Work

- Map each of the 9 Maven modules to a PiggyMetricsDotNet unit; do not merge account-service, auth-service, notification-service, statistics-service, turbine-stream-service.
- Port the OAuth2 grants and scopes (@EnableOAuth2Client on account-service, @EnableOAuth2Client on notification-service, @EnableOAuth2Client on statistics-service, @EnableResourceServer on auth-service) to ASP.NET Identity.
- Port the 3 Feign edges to HttpClient with the same paths and media types: account-service → auth-service, account-service → statistics-service, notification-service → account-service.
- Port the gateway routes (/uaa/**, /accounts/**, /statistics/**, /notifications/**) to YARP.
- Attach hours and tokens on every plan · done: None h · 0 tokens per plan.
- Cite no PiggyMetricsDotNet path until its index holds files.

### Todo

- [ ] Confirm scope and impacted assets
- [ ] Fill rollout and rollback
- [ ] Record the design — sandeep-chanda/piggymetricsJava has no architecture document indexed
- [ ] Migrate sandeep-chanda/piggymetricsJava
- [ ] Review sandeep-chanda/piggymetricsJava

## Test plan
1. Regression floor: the 23 test classes (59 tests) ported with the same names and assertions — Gateway.Tests (1 class · 2 tests); AuthService.Tests (5 classes · 9 tests); AccountService.Tests (5 classes · 14 tests); StatisticsService.Tests (6 classes · 16 tests); NotificationService.Tests (6 classes · 18 tests); the embedded store replaced by a container of the same store
2. Contract tests: one per Contract-map row (C1–C12), run against the destination alone with the seeded data; the row's check clause is the assertion
3. Auth matrix: for each of C1–C12, no token, a user token and a server token give the status the row's policy says (policies in play: anonymous; client credentials; server; server, or accountName == demo; server, or name == demo; user)
4. Parity replay (P7): the same requests against source and destination behind their gateways; JSON compared on the fields the callers read; dates compared as instants · E3 (statistics-service → ${rates.url}) stubbed identically on both sides
5. Edges with the callee down: E1 account-service → auth-service · propagates; E2 account-service → statistics-service · fallback · StatisticsServiceClientFallback; E4 notification-service → account-service · caught and logged in NotificationServiceImpl
6. Schedules: each fires on its cron against a seeded record that is due and does not fire against one that is not — NotificationServiceImpl.sendBackupNotifications (0 0 12 * * *); NotificationServiceImpl.sendRemindNotifications (0 0 0 * * *)
7. Gateway: each prefix (/uaa/**, /accounts/**, /statistics/**, /notifications/**) reaches its service and an unlisted path is 404; the static UI loads from the gateway host

## Rollout, cutover, rollback
### Rollout

1. Deploy AuthService, StatisticsService, AccountService, NotificationService, Gateway beside the source with their own hostnames; the source keeps serving
2. Run the parity harness (P7) until every contract row holds
3. Switch /accounts/**, then /statistics/**, then /notifications/**, then /uaa/** at the source gateway; the auth route last · the source gateway (gateway) stays the entry until the last route moves
4. Then move the static UI to the destination gateway and retire the source gateway

### Cutover

- Switch /accounts/**, then /statistics/**, then /notifications/**, then /uaa/** at the source gateway; the auth route last
- Shadow until each route's parity holds; roll a route back by pointing it at the source unit
- Data needs no rollback: both ends read and write the same stores

### Rollback

- Per route: point the source gateway's route back at the source unit (gateway routes by serviceId or url); the source runs shadow throughout so it is current
- Auth route: switching /uaa back re-issues tokens at the source; signed-in users sign in again
- Data: no rollback — both ends read and write the same stores in place

### Parity

- 12 contract rows replay through both gateways with the same seed data; a row holds parity when status and body match on the fields the callers read
- C1 · GET /uaa/users/current · GET with a user token → the source's body; without one → 401
- C2 · POST /uaa/users · POST with a server token → 200; with a user token → 403
- C3 · POST /uaa/oauth/token · each grant issues a token the resource servers accept
- C4 · GET /accounts/{name} · GET with a server token → 200; with a user token → 403; the named exception without a token → 200
- C5 · GET /accounts/current · GET with a user token → the source's body; without one → 401
- C6 · PUT /accounts/current · PUT with a user token → the source's body; without one → 401
- C7 · POST /accounts · POST without a token → the source's status and body
- C8 · GET /statistics/current · GET with a user token → the source's body; without one → 401
- C9 · GET /statistics/{accountName} · GET with a server token → 200; with a user token → 403; the named exception without a token → 200
- C10 · PUT /statistics/{accountName} · PUT with a server token → 200; with a user token → 403
- C11 · GET /notifications/recipients/current · GET with a user token → the source's body; without one → 401
- C12 · PUT /notifications/recipients/current · PUT with a user token → the source's body; without one → 401

### Guardrails

- H1: the ends are the Brief's — source sandeep-chanda/piggymetricsJava, destination sandeep-chanda/PiggyMetricsDotNet; never inferred from a name.
- Cite only paths the source index resolves until PiggyMetricsDotNet is indexed.
- No copybook counts on a Maven module repo: Inventory is the Maven module scan.

## Risks
R1 · E3 · statistics-service calls ${rates.url} outside the estate (GET /latest); on failure the source fallback · ExchangeRatesClientFallback. Both ends depend on it during parity: stub it identically or decide the fallback before P3 · statistics-service/src/main/java/com/piggymetrics/statistics/client/ExchangeRatesClient.java:L10
R2 · token validation is one call to http://auth-service:5000/uaa/users/current per request on account-service, statistics-service, notification-service; the destination inherits the latency and the single point of failure · config/src/main/resources/shared/application.yml:L23
R3 · 8 modules fail to start without the config server (monitoring, registry, gateway, auth-service, account-service, statistics-service, notification-service, turbine-stream-service); every key they read moves into their own settings before the first destination run · monitoring/src/main/resources/bootstrap.yml:L6
R4 · Account.lastSeen carries a time; source and destination must agree on zone and format during shadow or parity on accounts fails · account-service/src/main/java/com/piggymetrics/account/domain/Account.java:L13
R5 · DataPoint.id carries a time; source and destination must agree on zone and format during shadow or parity on datapoints fails · statistics-service/src/main/java/com/piggymetrics/statistics/domain/timeseries/DataPoint.java:L15
R6 · Recipient.scheduledNotifications carries a time; source and destination must agree on zone and format during shadow or parity on recipients fails · notification-service/src/main/java/com/piggymetrics/notification/domain/Recipient.java:L11
R7 · 2 schedules (NotificationServiceImpl.sendBackupNotifications, NotificationServiceImpl.sendRemindNotifications) would run on both ends during shadow; run them on one side only or their recipients are notified twice · notification-service/src/main/java/com/piggymetrics/notification/service/NotificationServiceImpl.java
R8 · E4 · notification-service → account-service failures are caught and logged in NotificationServiceImpl; the destination must swallow them the same way or notification-service's behaviour under a dead callee changes · notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java:L9
R9 · 4 credential values sit in configuration (config/src/main/resources/shared/account-service.yml, config/src/main/resources/shared/notification-service.yml, config/src/main/resources/shared/statistics-service.yml); the destination reads them from the environment — a deliberate difference the parity harness must supply · config/src/main/resources/shared/account-service.yml:L6

## Estimate basis
total: 191.201 usd
9 plans · $191.20 from tokencast for hm_harness. Human baseline 4 hours per task, one task per plan.
- P1 · $1.23 · tokencast · hm_harness · 4 files · medium · tokencast list · ×1.22 from 3 runs
- P2 · $42.85 · tokencast · hm_harness · 21 files · high · tokencast list · ×1.22 from 3 runs
- P3 · $46.28 · tokencast · hm_harness · 22 files · high · tokencast list · ×1.22 from 3 runs
- P4 · $46.28 · tokencast · hm_harness · 22 files · high · tokencast list · ×1.22 from 3 runs
- P5 · $46.28 · tokencast · hm_harness · 22 files · high · tokencast list · ×1.22 from 3 runs
- P6 · $4.83 · tokencast · hm_harness · 8 files · medium · tokencast list · ×1.22 from 3 runs
- P7 · $1.23 · tokencast · hm_harness · 4 files · medium · tokencast list · ×1.22 from 3 runs
- P8 · $0.63 · tokencast · hm_harness · 3 files · low · tokencast list · ×1.22 from 3 runs
- P9 · $1.60 · tokencast · hm_harness · 5 files · medium · tokencast list · ×1.22 from 3 runs

## Open questions
- 2 of 37 marker files were not readable
- @EnableGlobalMethodSecurity on account-service, auth-service, notification-service, statistics-service → ? · which PiggyMetricsDotNet equivalent, if any · account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L16
- @EnableScheduling on notification-service → ? · which PiggyMetricsDotNet equivalent, if any · notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L23

## Architecture
**Module view**

```mermaid
graph LR
  account_service[account-service]
  auth_service[auth-service]
  config[config]
  gateway[gateway]
  monitoring[monitoring]
  notification_service[notification-service]
  registry[registry]
  statistics_service[statistics-service]
  turbine_stream_service[turbine-stream-service]
  account_service -->|"@EnableDiscoveryClient"| registry
  auth_service -->|"@EnableDiscoveryClient"| registry
  gateway -->|"@EnableDiscoveryClient"| registry
  notification_service -->|"@EnableDiscoveryClient"| registry
  statistics_service -->|"@EnableDiscoveryClient"| registry
  turbine_stream_service -->|"@EnableDiscoveryClient"| registry
  account_service -->|"@FeignClient"| auth_service
  account_service -->|"@FeignClient"| statistics_service
  notification_service -->|"@FeignClient"| account_service
  account_service -->|"spring.cloud.config.uri"| config
  auth_service -->|"spring.cloud.config.uri"| config
  gateway -->|"zuul.routes"| auth_service
  gateway -->|"zuul.routes"| account_service
  gateway -->|"zuul.routes"| statistics_service
  gateway -->|"zuul.routes"| notification_service
  gateway -->|"spring.cloud.config.uri"| config
  monitoring -->|"spring.cloud.config.uri"| config
  notification_service -->|"spring.cloud.config.uri"| config
  registry -->|"spring.cloud.config.uri"| config
  statistics_service -->|"spring.cloud.config.uri"| config
  turbine_stream_service -->|"spring.cloud.config.uri"| config
```


**Container view**

```mermaid
C4Container
title Walk 24 Oct public migration
Container_Boundary(piggymetricsJavaBound, "sandeep-chanda/piggymetricsJava") {
    Container(piggymetricsJavaaccounts, "account-service", "Java", "")
    Container(piggymetricsJavaauthserv, "auth-service", "Java", "")
    Container(piggymetricsJavanotifica, "notification-service", "Java", "")
    Container(piggymetricsJavastatisti, "statistics-service", "Java", "")
    Container(piggymetricsJavaconfig, "config", "Java", "")
    Container(piggymetricsJavagateway, "gateway", "JavaScript", "")
    Container(piggymetricsJavaturbines, "turbine-stream-service", "Java", "")
    Container(piggymetricsJavaregistry, "registry", "Java", "")
    Container(piggymetricsJavamonitori, "monitoring", "Java", "")
}
```


**Component view**

```mermaid
C4Component
title account-service components
Container_Boundary(accountserviceComp, "account-service") {
    Component(cAccount, "Account", "", "")
    Component(cAccountServiceTest, "AccountServiceTest", "", "")
    Component(cAccountServiceImpl, "AccountServiceImpl", "", "")
    Component(cAccountControllerTes, "AccountControllerTest", "", "")
    Component(cUser, "User", "", "")
    Component(cAccountRepositoryTes, "AccountRepositoryTest", "", "")
    Component(cCurrency, "Currency", "", "")
    Component(cSaving, "Saving", "", "")
    Component(cAccountService, "AccountService", "", "")
    Component(cAuthServiceClient, "AuthServiceClient", "", "")
    Component(cStatisticsServiceCli, "StatisticsServiceClient", "", "")
    Component(cAccountController, "AccountController", "", "")
    Component(cItem, "Item", "", "")
    Component(cTimePeriod, "TimePeriod", "", "")
    Component(cAccountRepository, "AccountRepository", "", "")
    Component(cStatisticsServiceCli2, "StatisticsServiceClientFallback", "", "")
}
Rel(cAuthServiceClient, cUser, "1 import")
Rel(cStatisticsServiceCli2, cAccount, "1 import")
Rel(cStatisticsServiceCli, cAccount, "1 import")
Rel(cAccountController, cAccount, "1 import")
Rel(cAccountController, cUser, "1 import")
Rel(cAccountController, cAccountService, "1 import")
Rel(cAccountRepository, cAccount, "1 import")
Rel(cAccountServiceImpl, cUser, "1 import")
Rel(cAccountServiceImpl, cAccount, "1 import")
Rel(cAccountServiceImpl, cSaving, "1 import")
Rel(cAccountServiceImpl, cCurrency, "1 import")
Rel(cAccountServiceImpl, cStatisticsServiceCli, "1 import")
Rel(cAccountServiceImpl, cAccountRepository, "1 import")
Rel(cAccountServiceImpl, cAuthServiceClient, "1 import")
Rel(cAccountService, cAccount, "1 import")
Rel(cAccountService, cUser, "1 import")
Rel(cAccountControllerTes, cItem, "1 import")
Rel(cAccountControllerTes, cCurrency, "1 import")
Rel(cAccountControllerTes, cTimePeriod, "1 import")
Rel(cAccountControllerTes, cAccountService, "1 import")
```


**Module dependencies**

```mermaid
flowchart LR
  subgraph sandeep_chanda_piggymetricsJava["sandeep-chanda/piggymetricsJava"]
    src_test["src/test"]
    src_main["src/main"]
    src_test_x["src/test"]
    src_main_x["src/main"]
    src_test_x_x["src/test"]
    src_main_x_x["src/main"]
    src_test_x_x_x["src/test"]
    src_main_x_x_x["src/main"]
  end
  src_test -->|26 imports| src_main
  src_test_x -->|22 imports| src_main_x
  src_test_x_x -->|19 imports| src_main_x_x
  src_test_x_x_x -->|8 imports| src_main_x_x_x
```

- 9 Maven modules · 21 edges from the index · PiggyMetricsDotNet is not indexed
- account-service → registry · @EnableDiscoveryClient · account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L12
- auth-service → registry · @EnableDiscoveryClient · auth-service/src/main/java/com/piggymetrics/auth/AuthApplication.java:L11
- gateway → registry · @EnableDiscoveryClient · gateway/src/main/java/com/piggymetrics/gateway/GatewayApplication.java:L9
- notification-service → registry · @EnableDiscoveryClient · notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L19
- statistics-service → registry · @EnableDiscoveryClient · statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L22
- turbine-stream-service → registry · @EnableDiscoveryClient · turbine-stream-service/src/main/java/com/piggymetrics/turbine/TurbineStreamServiceApplication.java:L10
- account-service → auth-service · @FeignClient · account-service/src/main/java/com/piggymetrics/account/client/AuthServiceClient.java:L9
- account-service → statistics-service · @FeignClient · account-service/src/main/java/com/piggymetrics/account/client/StatisticsServiceClient.java:L10
- notification-service → account-service · @FeignClient · notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java:L9
- account-service → config · spring.cloud.config.uri · account-service/src/main/resources/bootstrap.yml:L6
- auth-service → config · spring.cloud.config.uri · auth-service/src/main/resources/bootstrap.yml:L6
- gateway → auth-service · zuul.routes · config/src/main/resources/shared/gateway.yml:L20
- gateway → account-service · zuul.routes · config/src/main/resources/shared/gateway.yml:L26
- gateway → statistics-service · zuul.routes · config/src/main/resources/shared/gateway.yml:L32
- gateway → notification-service · zuul.routes · config/src/main/resources/shared/gateway.yml:L38
- gateway → config · spring.cloud.config.uri · gateway/src/main/resources/bootstrap.yml:L6
- monitoring → config · spring.cloud.config.uri · monitoring/src/main/resources/bootstrap.yml:L6
- notification-service → config · spring.cloud.config.uri · notification-service/src/main/resources/bootstrap.yml:L6
- registry → config · spring.cloud.config.uri · registry/src/main/resources/bootstrap.yml:L6
- statistics-service → config · spring.cloud.config.uri · statistics-service/src/main/resources/bootstrap.yml:L6
- turbine-stream-service → config · spring.cloud.config.uri · turbine-stream-service/src/main/resources/bootstrap.yml:L6

## Related work
- Walk 23 Oct public v1 (published) — spec already covering this

## Citations
[^1]: sandeep-chanda/piggymetricsJava · README.md
[^2]: sandeep-chanda/piggymetricsJava · .harnessmatters/README.md
[^3]: sandeep-chanda/piggymetricsJava · account-service/pom.xml
[^4]: sandeep-chanda/piggymetricsJava · auth-service/pom.xml
[^5]: sandeep-chanda/piggymetricsJava · pom.xml
[^6]: sandeep-chanda/piggymetricsJava · .travis.yml
[^7]: sandeep-chanda/piggymetricsJava · config/pom.xml
[^8]: sandeep-chanda/piggymetricsJava · docker-compose.dev.yml
[^9]: sandeep-chanda/piggymetricsJava · docker-compose.yml
[^10]: sandeep-chanda/piggymetricsJava · gateway/pom.xml
[^11]: sandeep-chanda/piggymetricsJava · monitoring/pom.xml
[^12]: sandeep-chanda/piggymetricsJava · notification-service/pom.xml
[^13]: sandeep-chanda/piggymetricsJava · .harnessmatters/context/binding.json
[^14]: sandeep-chanda/piggymetricsJava · .harnessmatters/AGENTS.md
[^15]: sandeep-chanda/piggymetricsJava · AGENTS.md
[^16]: sandeep-chanda/piggymetricsJava · .harnessmatters/project.json
[^17]: sandeep-chanda/piggymetricsJava · .harnessmatters/README.md
[^18]: sandeep-chanda/piggymetricsJava · turbine-stream-service/src/main/resources/bootstrap.yml
[^19]: sandeep-chanda/piggymetricsJava · account-service/src/test/resources/application.yml
[^20]: sandeep-chanda/piggymetricsJava · docs/pass23-walk-fact.md
[^21]: sandeep-chanda/piggymetricsJava · pom.xml:L42
[^22]: sandeep-chanda/piggymetricsJava · pom.xml:L41
[^23]: sandeep-chanda/piggymetricsJava · pom.xml:L37
[^24]: sandeep-chanda/piggymetricsJava · pom.xml:L40
[^25]: sandeep-chanda/piggymetricsJava · pom.xml:L38
[^26]: sandeep-chanda/piggymetricsJava · pom.xml:L44
[^27]: sandeep-chanda/piggymetricsJava · pom.xml:L39
[^28]: sandeep-chanda/piggymetricsJava · pom.xml:L43
[^29]: sandeep-chanda/piggymetricsJava · pom.xml:L45
[^30]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L20
[^31]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L26
[^32]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L32
[^33]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L38
[^34]: sandeep-chanda/piggymetricsJava · pom.xml:L42
[^35]: sandeep-chanda/piggymetricsJava · pom.xml:L41
[^36]: sandeep-chanda/piggymetricsJava · pom.xml:L37
[^37]: sandeep-chanda/piggymetricsJava · pom.xml:L40
[^38]: sandeep-chanda/piggymetricsJava · pom.xml:L38
[^39]: sandeep-chanda/piggymetricsJava · pom.xml:L44
[^40]: sandeep-chanda/piggymetricsJava · pom.xml:L39
[^41]: sandeep-chanda/piggymetricsJava · pom.xml:L43
[^42]: sandeep-chanda/piggymetricsJava · pom.xml:L45
[^43]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L12
[^44]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/AuthApplication.java:L11
[^45]: sandeep-chanda/piggymetricsJava · gateway/src/main/java/com/piggymetrics/gateway/GatewayApplication.java:L9
[^46]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L19
[^47]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L22
[^48]: sandeep-chanda/piggymetricsJava · turbine-stream-service/src/main/java/com/piggymetrics/turbine/TurbineStreamServiceApplication.java:L10
[^49]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/client/AuthServiceClient.java:L9
[^50]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/client/StatisticsServiceClient.java:L10
[^51]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java:L9
[^52]: sandeep-chanda/piggymetricsJava · account-service/src/main/resources/bootstrap.yml:L6
[^53]: sandeep-chanda/piggymetricsJava · auth-service/src/main/resources/bootstrap.yml:L6
[^54]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L20
[^55]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L26
[^56]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L32
[^57]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L38
[^58]: sandeep-chanda/piggymetricsJava · gateway/src/main/resources/bootstrap.yml:L6
[^59]: sandeep-chanda/piggymetricsJava · monitoring/src/main/resources/bootstrap.yml:L6
[^60]: sandeep-chanda/piggymetricsJava · notification-service/src/main/resources/bootstrap.yml:L6
[^61]: sandeep-chanda/piggymetricsJava · registry/src/main/resources/bootstrap.yml:L6
[^62]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/resources/bootstrap.yml:L6
[^63]: sandeep-chanda/piggymetricsJava · turbine-stream-service/src/main/resources/bootstrap.yml:L6
[^64]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L11
[^65]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/AuthApplication.java:L9
[^66]: sandeep-chanda/piggymetricsJava · config/src/main/java/com/piggymetrics/config/ConfigApplication.java:L7
[^67]: sandeep-chanda/piggymetricsJava · gateway/src/main/java/com/piggymetrics/gateway/GatewayApplication.java:L8
[^68]: sandeep-chanda/piggymetricsJava · monitoring/src/main/java/com/piggymetrics/monitoring/MonitoringApplication.java:L7
[^69]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L18
[^70]: sandeep-chanda/piggymetricsJava · registry/src/main/java/com/piggymetrics/registry/RegistryApplication.java:L7
[^71]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L21
[^72]: sandeep-chanda/piggymetricsJava · turbine-stream-service/src/main/java/com/piggymetrics/turbine/TurbineStreamServiceApplication.java:L8
[^73]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L11
[^74]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/AuthApplication.java:L9
[^75]: sandeep-chanda/piggymetricsJava · config/src/main/java/com/piggymetrics/config/ConfigApplication.java:L7
[^76]: sandeep-chanda/piggymetricsJava · gateway/src/main/java/com/piggymetrics/gateway/GatewayApplication.java:L8
[^77]: sandeep-chanda/piggymetricsJava · monitoring/src/main/java/com/piggymetrics/monitoring/MonitoringApplication.java:L7
[^78]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L18
[^79]: sandeep-chanda/piggymetricsJava · registry/src/main/java/com/piggymetrics/registry/RegistryApplication.java:L7
[^80]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L21
[^81]: sandeep-chanda/piggymetricsJava · turbine-stream-service/src/main/java/com/piggymetrics/turbine/TurbineStreamServiceApplication.java:L8
[^82]: sandeep-chanda/piggymetricsJava · registry/src/main/java/com/piggymetrics/registry/RegistryApplication.java:L8
[^83]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L12
[^84]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/AuthApplication.java:L11
[^85]: sandeep-chanda/piggymetricsJava · gateway/src/main/java/com/piggymetrics/gateway/GatewayApplication.java:L9
[^86]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L19
[^87]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L22
[^88]: sandeep-chanda/piggymetricsJava · turbine-stream-service/src/main/java/com/piggymetrics/turbine/TurbineStreamServiceApplication.java:L10
[^89]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L14
[^90]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/client/AuthServiceClient.java:L9
[^91]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/client/StatisticsServiceClient.java:L10
[^92]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L21
[^93]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java:L9
[^94]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L24
[^95]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/client/ExchangeRatesClient.java:L10
[^96]: sandeep-chanda/piggymetricsJava · gateway/src/main/java/com/piggymetrics/gateway/GatewayApplication.java:L10
[^97]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L13
[^98]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/AuthApplication.java:L10
[^99]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L20
[^100]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L23
[^101]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L15
[^102]: sandeep-chanda/piggymetricsJava · monitoring/src/main/java/com/piggymetrics/monitoring/MonitoringApplication.java:L8
[^103]: sandeep-chanda/piggymetricsJava · config/src/main/java/com/piggymetrics/config/ConfigApplication.java:L8
[^104]: sandeep-chanda/piggymetricsJava · turbine-stream-service/src/main/java/com/piggymetrics/turbine/TurbineStreamServiceApplication.java:L9
[^105]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L16
[^106]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/AuthApplication.java:L12
[^107]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L22
[^108]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L25
[^109]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L23
[^110]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L12
[^111]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L13
[^112]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L14
[^113]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L15
[^114]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/AccountApplication.java:L16
[^115]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/AuthApplication.java:L10
[^116]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/AuthApplication.java:L11
[^117]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/AuthApplication.java:L12
[^118]: sandeep-chanda/piggymetricsJava · config/src/main/java/com/piggymetrics/config/ConfigApplication.java:L8
[^119]: sandeep-chanda/piggymetricsJava · gateway/src/main/java/com/piggymetrics/gateway/GatewayApplication.java:L9
[^120]: sandeep-chanda/piggymetricsJava · gateway/src/main/java/com/piggymetrics/gateway/GatewayApplication.java:L10
[^121]: sandeep-chanda/piggymetricsJava · monitoring/src/main/java/com/piggymetrics/monitoring/MonitoringApplication.java:L8
[^122]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L19
[^123]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L20
[^124]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L21
[^125]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L22
[^126]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/NotificationServiceApplication.java:L23
[^127]: sandeep-chanda/piggymetricsJava · registry/src/main/java/com/piggymetrics/registry/RegistryApplication.java:L8
[^128]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L22
[^129]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L23
[^130]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L24
[^131]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/StatisticsApplication.java:L25
[^132]: sandeep-chanda/piggymetricsJava · turbine-stream-service/src/main/java/com/piggymetrics/turbine/TurbineStreamServiceApplication.java:L9
[^133]: sandeep-chanda/piggymetricsJava · turbine-stream-service/src/main/java/com/piggymetrics/turbine/TurbineStreamServiceApplication.java:L10
[^134]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/client/AuthServiceClient.java:L9
[^135]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/client/StatisticsServiceClient.java:L10
[^136]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java:L9
[^137]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/client/ExchangeRatesClient.java:L10
[^138]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L20
[^139]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L25
[^140]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L30
[^141]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L35
[^142]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L16
[^143]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L22
[^144]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L28
[^145]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L15
[^146]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L21
[^147]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L26
[^148]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L20
[^149]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L26
[^150]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L32
[^151]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L20
[^152]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L26
[^153]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L32
[^154]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L38
[^155]: sandeep-chanda/piggymetricsJava · pom.xml:L42
[^156]: sandeep-chanda/piggymetricsJava · pom.xml:L41
[^157]: sandeep-chanda/piggymetricsJava · pom.xml:L37
[^158]: sandeep-chanda/piggymetricsJava · pom.xml:L40
[^159]: sandeep-chanda/piggymetricsJava · pom.xml:L38
[^160]: sandeep-chanda/piggymetricsJava · pom.xml:L44
[^161]: sandeep-chanda/piggymetricsJava · pom.xml:L39
[^162]: sandeep-chanda/piggymetricsJava · pom.xml:L43
[^163]: sandeep-chanda/piggymetricsJava · pom.xml:L45
[^164]: sandeep-chanda/piggymetricsJava · account-service/src/main/resources/bootstrap.yml:L7
[^165]: sandeep-chanda/piggymetricsJava · auth-service/src/main/resources/bootstrap.yml:L7
[^166]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/account-service.yml:L13
[^167]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/account-service.yml:L26
[^168]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/application.yml:L11
[^169]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/application.yml:L27
[^170]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/auth-service.yml:L4
[^171]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L7
[^172]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L10
[^173]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L11
[^174]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L16
[^175]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L17
[^176]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/notification-service.yml:L31
[^177]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/statistics-service.yml:L13
[^178]: sandeep-chanda/piggymetricsJava · gateway/src/main/resources/bootstrap.yml:L7
[^179]: sandeep-chanda/piggymetricsJava · monitoring/src/main/resources/bootstrap.yml:L7
[^180]: sandeep-chanda/piggymetricsJava · notification-service/src/main/resources/bootstrap.yml:L7
[^181]: sandeep-chanda/piggymetricsJava · registry/src/main/resources/bootstrap.yml:L7
[^182]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/resources/bootstrap.yml:L7
[^183]: sandeep-chanda/piggymetricsJava · turbine-stream-service/src/main/resources/bootstrap.yml:L7
[^184]: sandeep-chanda/piggymetricsJava · config/src/main/resources/application.yml:L14
[^185]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/registry.yml:L2
[^186]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L45
[^187]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/auth-service.yml:L13
[^188]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L22
[^189]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/domain/User.java:L10
[^190]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/account-service.yml:L22
[^191]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L20
[^192]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/domain/Account.java:L13
[^193]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/client/AuthServiceClient.java:L9
[^194]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/statistics-service.yml:L22
[^195]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L20
[^196]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/domain/Account.java:L10
[^197]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/client/ExchangeRatesClient.java:L10
[^198]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/notification-service.yml:L13
[^199]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L21
[^200]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/domain/Recipient.java:L11
[^201]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java:L9
[^202]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/service/NotificationServiceImpl.java:L31
[^203]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L22
[^204]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L28
[^205]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L20
[^206]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L25
[^207]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L30
[^208]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L35
[^209]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L20
[^210]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L26
[^211]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L32
[^212]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L21
[^213]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L26
[^214]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/domain/User.java:L10
[^215]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/domain/Account.java:L13
[^216]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/domain/Account.java:L10
[^217]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/domain/timeseries/DataPoint.java:L15
[^218]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/domain/Recipient.java:L11
[^219]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/client/AuthServiceClient.java:L9
[^220]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/client/StatisticsServiceClient.java:L10
[^221]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/client/ExchangeRatesClient.java:L10
[^222]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java:L9
[^223]: sandeep-chanda/piggymetricsJava · monitoring/src/main/resources/bootstrap.yml:L6
[^224]: sandeep-chanda/piggymetricsJava · registry/src/main/resources/bootstrap.yml:L6
[^225]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L21
[^226]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L27
[^227]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L33
[^228]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L39
[^229]: sandeep-chanda/piggymetricsJava · gateway/src/main/resources/bootstrap.yml:L6
[^230]: sandeep-chanda/piggymetricsJava · auth-service/src/main/resources/bootstrap.yml:L6
[^231]: sandeep-chanda/piggymetricsJava · account-service/src/main/resources/bootstrap.yml:L6
[^232]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/application.yml:L27
[^233]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/application.yml:L23
[^234]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/resources/bootstrap.yml:L6
[^235]: sandeep-chanda/piggymetricsJava · notification-service/src/main/resources/bootstrap.yml:L6
[^236]: sandeep-chanda/piggymetricsJava · turbine-stream-service/src/main/resources/bootstrap.yml:L6
[^237]: sandeep-chanda/piggymetricsJava · account-service/pom.xml:L1
[^238]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/config/ResourceServerConfig.java:L1
[^239]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/repository/AccountRepository.java:L1
[^240]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/domain/User.java:L10
[^241]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/repository/UserRepository.java:L1
[^242]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L22
[^243]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L28
[^244]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/config/OAuth2AuthorizationConfig.java:L1
[^245]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/auth-service.yml:L1
[^246]: sandeep-chanda/piggymetricsJava · auth-service/src/test/java/com/piggymetrics/auth/AuthServiceApplicationTests.java:L1
[^247]: sandeep-chanda/piggymetricsJava · auth-service/src/test/java/com/piggymetrics/auth/controller/UserControllerTest.java:L1
[^248]: sandeep-chanda/piggymetricsJava · auth-service/src/test/java/com/piggymetrics/auth/repository/UserRepositoryTest.java:L1
[^249]: sandeep-chanda/piggymetricsJava · auth-service/src/test/java/com/piggymetrics/auth/service/UserServiceTest.java:L1
[^250]: sandeep-chanda/piggymetricsJava · auth-service/src/test/java/com/piggymetrics/auth/service/security/MongoUserDetailsServiceTest.java:L1
[^251]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/domain/Account.java:L10
[^252]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/domain/timeseries/DataPoint.java:L15
[^253]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/repository/DataPointRepository.java:L1
[^254]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L20
[^255]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L26
[^256]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L32
[^257]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/client/ExchangeRatesClient.java:L1
[^258]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/resources/bootstrap.yml:L1
[^259]: sandeep-chanda/piggymetricsJava · statistics-service/src/test/java/com/piggymetrics/statistics/StatisticsServiceApplicationTests.java:L1
[^260]: sandeep-chanda/piggymetricsJava · statistics-service/src/test/java/com/piggymetrics/statistics/client/ExchangeRatesClientTest.java:L1
[^261]: sandeep-chanda/piggymetricsJava · statistics-service/src/test/java/com/piggymetrics/statistics/controller/StatisticsControllerTest.java:L1
[^262]: sandeep-chanda/piggymetricsJava · statistics-service/src/test/java/com/piggymetrics/statistics/repository/DataPointRepositoryTest.java:L1
[^263]: sandeep-chanda/piggymetricsJava · statistics-service/src/test/java/com/piggymetrics/statistics/service/ExchangeRatesServiceImplTest.java:L1
[^264]: sandeep-chanda/piggymetricsJava · statistics-service/src/test/java/com/piggymetrics/statistics/service/StatisticsServiceImplTest.java:L1
[^265]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/domain/Account.java:L13
[^266]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L20
[^267]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L25
[^268]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L30
[^269]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L35
[^270]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/client/AuthServiceClient.java:L1
[^271]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/client/StatisticsServiceClient.java:L1
[^272]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/account-service.yml:L1
[^273]: sandeep-chanda/piggymetricsJava · account-service/src/test/java/com/piggymetrics/account/AccountServiceApplicationTests.java:L1
[^274]: sandeep-chanda/piggymetricsJava · account-service/src/test/java/com/piggymetrics/account/client/StatisticsServiceClientFallbackTest.java:L1
[^275]: sandeep-chanda/piggymetricsJava · account-service/src/test/java/com/piggymetrics/account/controller/AccountControllerTest.java:L1
[^276]: sandeep-chanda/piggymetricsJava · account-service/src/test/java/com/piggymetrics/account/repository/AccountRepositoryTest.java:L1
[^277]: sandeep-chanda/piggymetricsJava · account-service/src/test/java/com/piggymetrics/account/service/AccountServiceTest.java:L1
[^278]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/domain/Recipient.java:L11
[^279]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/repository/RecipientRepository.java:L1
[^280]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L21
[^281]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L26
[^282]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java:L1
[^283]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/service/NotificationServiceImpl.java:L1
[^284]: sandeep-chanda/piggymetricsJava · notification-service/src/main/resources/bootstrap.yml:L1
[^285]: sandeep-chanda/piggymetricsJava · notification-service/src/test/java/com/piggymetrics/notification/NotificationServiceApplicationTests.java:L1
[^286]: sandeep-chanda/piggymetricsJava · notification-service/src/test/java/com/piggymetrics/notification/controller/RecipientControllerTest.java:L1
[^287]: sandeep-chanda/piggymetricsJava · notification-service/src/test/java/com/piggymetrics/notification/repository/RecipientRepositoryTest.java:L1
[^288]: sandeep-chanda/piggymetricsJava · notification-service/src/test/java/com/piggymetrics/notification/service/EmailServiceImplTest.java:L1
[^289]: sandeep-chanda/piggymetricsJava · notification-service/src/test/java/com/piggymetrics/notification/service/NotificationServiceImplTest.java:L1
[^290]: sandeep-chanda/piggymetricsJava · notification-service/src/test/java/com/piggymetrics/notification/service/RecipientServiceImplTest.java:L1
[^291]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L21
[^292]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L27
[^293]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L33
[^294]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L39
[^295]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L1
[^296]: sandeep-chanda/piggymetricsJava · gateway/src/main/resources/bootstrap.yml:L1
[^297]: sandeep-chanda/piggymetricsJava · gateway/src/test/java/com/piggymetrics/gateway/GatewayApplicationTests.java:L1
[^298]: sandeep-chanda/piggymetricsJava · config/src/main/resources/application.yml:L14
[^299]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/registry.yml:L2
[^300]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/gateway.yml:L45
[^301]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L22
[^302]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/config/OAuth2AuthorizationConfig.java:L1
[^303]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/domain/User.java:L10
[^304]: sandeep-chanda/piggymetricsJava · auth-service/src/main/java/com/piggymetrics/auth/repository/UserRepository.java:L1
[^305]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/auth-service.yml:L13
[^306]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/controller/AccountController.java:L20
[^307]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/domain/Account.java:L13
[^308]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/repository/AccountRepository.java:L1
[^309]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/client/AuthServiceClient.java:L9
[^310]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/client/StatisticsServiceClient.java:L10
[^311]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/account-service.yml:L22
[^312]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L20
[^313]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/domain/Account.java:L10
[^314]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/domain/timeseries/DataPoint.java:L15
[^315]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/repository/DataPointRepository.java:L1
[^316]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/client/ExchangeRatesClient.java:L10
[^317]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/statistics-service.yml:L22
[^318]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/controller/RecipientController.java:L21
[^319]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/domain/Recipient.java:L11
[^320]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/repository/RecipientRepository.java:L1
[^321]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java:L9
[^322]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/service/NotificationServiceImpl.java:L31
[^323]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/notification-service.yml:L13
[^324]: sandeep-chanda/piggymetricsJava · account-service/pom.xml:L1
[^325]: sandeep-chanda/piggymetricsJava · auth-service/pom.xml:L1
[^326]: sandeep-chanda/piggymetricsJava · config/pom.xml:L1
[^327]: sandeep-chanda/piggymetricsJava · gateway/pom.xml:L1
[^328]: sandeep-chanda/piggymetricsJava · monitoring/pom.xml:L1
[^329]: sandeep-chanda/piggymetricsJava · notification-service/pom.xml:L1
[^330]: sandeep-chanda/piggymetricsJava · pom.xml:L1
[^331]: sandeep-chanda/piggymetricsJava · registry/pom.xml:L1
[^332]: sandeep-chanda/piggymetricsJava · statistics-service/pom.xml:L1
[^333]: sandeep-chanda/piggymetricsJava · turbine-stream-service/pom.xml:L1
[^334]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/client/ExchangeRatesClient.java:L10
[^335]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/application.yml:L23
[^336]: sandeep-chanda/piggymetricsJava · monitoring/src/main/resources/bootstrap.yml:L6
[^337]: sandeep-chanda/piggymetricsJava · account-service/src/main/java/com/piggymetrics/account/domain/Account.java:L13
[^338]: sandeep-chanda/piggymetricsJava · statistics-service/src/main/java/com/piggymetrics/statistics/domain/timeseries/DataPoint.java:L15
[^339]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/domain/Recipient.java:L11
[^340]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/service/NotificationServiceImpl.java:L1
[^341]: sandeep-chanda/piggymetricsJava · notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java:L9
[^342]: sandeep-chanda/piggymetricsJava · config/src/main/resources/shared/account-service.yml:L6
[^343]: spec:68eaf8d8-7663-4dfd-bde1-5e99b74829f7
