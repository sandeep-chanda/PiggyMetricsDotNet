# PRODUCT SPECIFICATION & IMPLEMENTATION TASK

Spec: Walk 24 Oct migration
Plan key: P1

## Current Task
Implement **Walk 24 Oct migration** plan **P1 · Solution skeleton and Shared**.

- P1 · Solution skeleton and Shared
  the .NET solution every service plan builds on
- Target: Shared · .NET
- Done when: every project builds; Shared's bearer handler accepts a token the source's authorization server issued and refuses one it did not
  - P1.T1 · Solution and one project per runtime service (Gateway, AuthService, AccountService, StatisticsService, NotificationService) plus Shared · .NET · from account-service/pom.xml
  - P1.T2 · Shared · bearer handler that validates a token the way the resource servers do · calls http://auth-service:5000/uaa/users/current · account-service ResourceServerConfig / CustomUserInfoTokenServices
  - P1.T3 · Shared · store conventions and serializers for the documents' field names and ids · MongoDB.Driver · same BSON field names and _id semantics
  - P1.T4 · Shared · typed-client base with the source's timeout (hystrix 10000ms) and a client-credentials token cache
  - P1.T5 · Shared · health checks and JSON options (enums as names, unknown fields ignored, the source's date format)

Gate floor: strongest acceptance runs tests (`dotnet test`).
