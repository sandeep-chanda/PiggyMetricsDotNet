# PRODUCT SPECIFICATION & IMPLEMENTATION TASK

Spec: Migration Java 2 .NET v18
Plan key: P1
Target repo: PiggyMetricsDotNet

## Current Task
Implement Migration Java 2 .NET v18: every plan and todo below, in plan order.
Commit messages name the todo ids they complete (P<n>.T<m>).

- P1. Solution skeleton and Shared. Target Shared .NET.
  Done when: every project builds; Shared bearer handler accepts a token the source authorization server issued and refuses one it did not.
  - P1.T1 Solution and one project per runtime service (Gateway, AuthService, AccountService, StatisticsService, NotificationService) plus Shared.
  - P1.T2 Shared bearer handler calls http://auth-service:5000/uaa/users/current.
  - P1.T3 Shared store conventions and serializers for document field names and ids. Same databases and collections.
  - P1.T4 Shared typed-client base with the source timeout and a client-credentials token cache. One HTTP client per edge.
  - P1.T5 Shared health checks and JSON options (enums as names, unknown fields ignored, the source date format).
- P2. AuthService. Port 5000. Path /uaa. Store auth-mongodb. Depends on P1.
  Done when: C1-C3 replay through AuthService with the source status and body; the 5 ported test classes (9 tests) are green.
  - P2.T1 User collection users, _id = username, fields username and password.
  - P2.T2 UserRepository on auth-mongodb.
  - P2.T3 C1 GET /uaa/users/current policy user. With a user token returns the source body. Without a token returns 401.
  - P2.T4 C2 POST /uaa/users policy server. Server token returns 200. User token returns 403.
  - P2.T5 C3 POST /uaa/oauth/token grants client_credentials, password, refresh_token. Each grant issues a token resource servers accept.
  - P2.T6 Four clients and their grants and scopes.
  - P2.T7 Settings port 5000, path /uaa, store auth-mongodb/piggymetrics.
  - P2.T8 through P2.T12 port AuthServiceApplicationTests, UserControllerTest, UserRepositoryTest, UserServiceTest, MongoUserDetailsServiceTest.
- P3. StatisticsService. Port 7000. Path /statistics. Store statistics-mongodb. Depends on P1.
  Done when: C8-C10 replay; E3 falls back when the callee is down; the 6 ported test classes (16 tests) are green.
  - P3.T1 Account request shape incomes, expenses, saving.
  - P3.T2 DataPoint collection datapoints, compound id, fields incomes, expenses, statistics, rates.
  - P3.T3 DataPointRepository on statistics-mongodb.
  - P3.T4 C8 GET /statistics/current policy user.
  - P3.T5 C9 GET /statistics/{accountName} policy server, or accountName demo.
  - P3.T6 C10 PUT /statistics/{accountName} policy server.
  - P3.T7 Client GET /latest on rates-client with fallback.
  - P3.T8 Settings port 7000, path /statistics, store statistics-mongodb/piggymetrics.
  - P3.T9 through P3.T14 port the six test classes named in the source tree.
