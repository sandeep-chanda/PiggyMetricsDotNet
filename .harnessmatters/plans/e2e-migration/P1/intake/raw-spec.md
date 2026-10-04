# PRODUCT SPECIFICATION & IMPLEMENTATION TASK

Spec: E2E migration
Plan key (this planning run): P1
Target repo: PiggyMetricsDotNet

## Current Task
Implement **E2E migration**: every plan and todo below, in plan order.
Commit messages name the todo ids they complete (`P<n>.T<m>`).

- P1 - Solution skeleton and Shared
  the .NET solution every service plan builds on
- Target: Shared - .NET
- Done when: every project builds; Shared's bearer handler accepts a token the source's authorization server issued and refuses one it did not
  - P1.T1 - Solution and one project per runtime service (Gateway, AuthService, AccountService, StatisticsService, NotificationService) plus Shared - .NET - from account-service/pom.xml
  - P1.T2 - Shared - bearer handler that validates a token the way the resource servers do - calls http://auth-service:5000/uaa/users/current - account-service/src/main/java/com/piggymetrics/account/config/ResourceServerConfig.java
  - P1.T3 - Shared - store conventions and serializers for the documents' field names and ids - the same databases and collections - account-service/src/main/java/com/piggymetrics/account/repository/AccountRepository.java
  - P1.T4 - Shared - typed-client base with the source's timeout and a client-credentials token cache - an HTTP client per edge - timeout matching the source
  - P1.T5 - Shared - health checks and JSON options (enums as names, unknown fields ignored, the source's date format)
- P2 - AuthService - auth-service (depends on P1)
- P3 - StatisticsService - statistics-service (depends on P1)
- P4+ - AccountService, NotificationService, Gateway (depend on P1)

Downstream plans P2..Pn are out of scope for this planning run; P1 is the plan key under plan.
