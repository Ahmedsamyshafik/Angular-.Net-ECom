# FullStack E-Commerce — ASP.NET Core 10 + Angular 21

A full-stack e-commerce sample: a versioned ASP.NET Core Web API backed by SQL Server and Redis, and an Angular single-page storefront.

> The Angular client lives in a separate repository: <https://github.com/Ahmedsamyshafik/ECom-Angular>. This README covers the backend repository and the full request flow; setup steps for the client are in [Getting started](#getting-started).

## Tech stack

| Area | Technology |
| --- | --- |
| Backend | ASP.NET Core 10 (`net10.0`) |
| Client | Angular 21 (separate repository) |
| Database | SQL Server with EF Core 10 |
| Cache / cart | Redis (`StackExchange.Redis`) |
| Identity | ASP.NET Core Identity; JWT delivered in an `HttpOnly` cookie |
| Payments | Stripe Checkout (`Stripe.net`) |
| Background jobs | Hangfire with SQL Server storage |
| Validation | FluentValidation |
| Tests | xUnit + Moq (plus EF Core InMemory/SQLite and Hangfire memory storage) |
| Also used | AutoMapper, MailKit/MimeKit (SMTP email) |

## Architecture

| Project | Responsibility |
| --- | --- |
| `Ecom.Core` | Entities, DTOs, repository/service interfaces, mapping profiles, domain exceptions. |
| `ECom.Infrastructure` | `AppDbContext` + migrations, Identity/JWT, Redis cart, stock reservation ledger, Hangfire jobs, Stripe/email services, repositories, business services. |
| `ECom.Api` | ASP.NET Core host: controllers, DI composition, FluentValidation, exception handling, security headers, rate limiting, API versioning, OpenAPI/Scalar. |
| Angular client (separate repo) | Storefront SPA: catalog, basket, checkout, Identity screens, Stripe redirect, payment-success polling. |
| `ECom.Api.Tests` | xUnit/Moq unit tests and real-SQL integration tests. |

Main flow:

1. Cart lines live in Redis, keyed by the authenticated user id or a server-issued guest id.
2. Adding or changing a line adjusts a stock reservation in SQL Server (conditional update) and schedules its expiration.
3. `POST /api/v1/Order/CreateOrder` snapshots product prices, creates the order and items, attaches the reservations, then clears the Redis cart.
4. `POST /api/v1/Payment/GeneratePayment` creates a Stripe Checkout Session, including the delivery fee as a line item.
5. Stripe calls `POST /api/v1/Payment/Webhook`; the signature is verified, every reservation is confirmed, and only then is the order marked paid.
6. Hangfire expires abandoned reservations after 3 days (one-shot job plus a minutely sweep).

## Highlight: Stock Reservation Ledger

`StockReservationService` is the single owner of stock mutations. The design avoids read-then-write races:

- **Conditional update, not read-then-write.** Reserving runs `UPDATE TblProducts SET Quantity = Quantity - @delta WHERE Id = @id AND IsDeleted = 0 AND Quantity >= @delta`. If zero rows change, the request fails with `400` and the transaction rolls back. Two concurrent requests for the last unit serialize on the row, so only one can consume it.
- **One active reservation per user/product.** A filtered unique index (`IX_TblStockReservations_ActiveUserProduct` on `UserId, ProductId`, filter `[Status] IN ('Pending','AttachedToOrder')`) enforces it in the database; released/expired/confirmed history rows are unconstrained.
- **RowVersion on order attach.** `BaseEntity.RowVersion` is a SQL Server `rowversion`. When an order attaches its reservations, a concurrent expiry or renewal makes `SaveChanges` throw `DbUpdateConcurrencyException`, which is mapped to `409 Conflict` and rolls the request back.
- **3-day TTL.** New reservations get `ExpiresAt = now + 3 days`.
- **One-shot job plus a sweep.** Each newly created reservation schedules one Hangfire job; a minutely recurring job (`expire-stock-reservations`) catches reservations whose job was lost. Both call `ExpireReservationAsync`.
- **Idempotent release.** `ExpireReservationAsync` only releases when its conditional `Status` update changes exactly one row, so a repeated or late run restores stock once and never releases an already-paid order.
- **Real-SQL tests.** `StockReservationServiceSqlTests` runs against SQL Server to exercise the conditional T-SQL, the filtered index, and RowVersion behavior. It uses a dedicated database (`AngularDotNetEcom_ReservationTests`) and never the application database.

## Payment flow

- **Checkout Session.** `POST /api/v1/Payment/GeneratePayment` loads the signed-in user's pending order and creates a Stripe Checkout Session (`card`, USD). Line items are the order items plus the delivery fee, so the Stripe total matches `TblOrders.GetTotal()` (`SubTotal + deliveryMethod.Price`). The fee line is skipped when the price is `0`.
- **Signed webhook.** `POST /api/v1/Payment/Webhook` is anonymous but verifies the `Stripe-Signature` header with `Stripe:WebhookSecret` (`EventUtility.ConstructEvent`). Only `checkout.session.completed` with `PaymentStatus == "paid"` is processed.
- **Confirm before marking paid.** `GetReservationThenConfirm` confirms *all* reservations attached to the order. `UpdateOrderToConfirm` is called only when every reservation was confirmed. If confirmation fails (for example, the 3-day window expired and stock was released), the order is **not** marked paid and a warning with the order id and Stripe session id is logged so the payment can be refunded manually. The endpoint still returns `200` so Stripe stops retrying.
- **Success page trusts the server.** After the redirect, the Angular success page polls `GET /api/v1/Order/GetAllOrders` (6 attempts, 3 seconds apart) and only shows "Payment confirmed" when the API reports `PaymentReceived` for that order. It never treats the `session_id` query parameter as proof of payment.

## Features

- **Catalog:** anonymous product/category reads; product search (all terms must match), category, min/max price filters, sorting, and pagination.
- **Discounts:** percentage or fixed-amount discounts with an active date window; applied per product or per category; the lowest resulting price wins.
- **Cart:** Redis-backed; quantities are reconciled with stock reservations; guest carts are supported by the API via a server-issued guest cookie.
- **Orders:** price snapshots at creation, delivery methods, status tracking.
- **Payments:** Stripe Checkout and a signature-verified webhook.
- **Identity:** register, email confirmation, login (JWT in an HttpOnly cookie), forgot/reset password, change password, and a saved address.
- **Authorization:** catalog write endpoints require the `Admin` role; catalog reads are anonymous; orders and payment require authentication; the cart also works for guests.
- **API quality:** ProblemDetails error contract with a global exception handler (`400/401/403/404/409/500`), security headers, global and per-client rate limiting, FluentValidation, and API versioning (URL segment and `api-version` header).
- **Developer docs:** OpenAPI and Scalar UI in Development at `/scalar/v1`.

## Getting started

### Prerequisites

- .NET 10 SDK
- SQL Server (any edition reachable from the API)
- Redis
- Node.js and npm (for the Angular client)
- A Stripe test account (only for the payment flow)
- An SMTP account (only for registration/reset emails)

### Configuration

Set the following keys in `Src/ECom.Api/appsettings.Development.json`, environment variables, or .NET user secrets. Use placeholders locally and never commit real credentials.

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | SQL Server connection string. |
| `Redis` | Redis endpoint, for example `<REDIS_HOST>:6379`. |
| `token:Secret` | JWT signing secret (at least 32 bytes for HS256). |
| `token:Issure` | JWT issuer (the key is spelled `Issure` in the code). |
| `Stripe:PublishKey` | Stripe publishable key. |
| `Stripe:SecretKey` | Stripe secret key (server-side only). |
| `Stripe:WebhookSecret` | Stripe webhook signing secret. |
| `EmailSetting:Port` | SMTP port (the code uses SSL on connect; the sample value is `465`). |
| `EmailSetting:From` | Sender address. |
| `EmailSetting:username` | SMTP username. |
| `EmailSetting:password` | SMTP password. |
| `EmailSetting:smtp` | SMTP host. |
| `Seed:AdminEmail` | Optional; promotes an existing account to `Admin` at startup. |
| `RateLimiting:*` | Optional; defaults are in `RateLimitingRegisteration`. |

Example shape (replace every `<...>`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=<SQL_HOST>;Database=<DATABASE_NAME>;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Redis": "<REDIS_HOST>:6379",
  "Seed": { "AdminEmail": "<ADMIN_EMAIL_OR_EMPTY>" },
  "EmailSetting": {
    "Port": 465,
    "From": "<SMTP_FROM_ADDRESS>",
    "username": "<SMTP_USERNAME>",
    "password": "<SMTP_PASSWORD>",
    "smtp": "<SMTP_HOST>"
  },
  "token": {
    "Secret": "<JWT_SIGNING_SECRET>",
    "Issure": "<JWT_ISSUER>"
  },
  "Stripe": {
    "PublishKey": "<STRIPE_PUBLISHABLE_KEY>",
    "SecretKey": "<STRIPE_SECRET_KEY>",
    "WebhookSecret": "<STRIPE_WEBHOOK_SECRET>"
  }
}
```

At startup the app creates the `Admin` role if it is missing. No default users or passwords are created; `Seed:AdminEmail` only promotes an account that already exists.

### Database

The EF Core tool is pinned in `dotnet-tools.json`:

```bash
dotnet tool restore
dotnet ef database update \
  --project Src/ECom.Infrastructure/ECom.Infrastructure.csproj \
  --startup-project Src/ECom.Api/ECom.Api.csproj
```

### Run the API

```bash
dotnet run --project Src/ECom.Api/ECom.Api.csproj
```

- HTTP: <http://localhost:5090>
- HTTPS profile: <https://localhost:7256>
- OpenAPI/Scalar (Development): <http://localhost:5090/scalar/v1>

### Run the Angular client

The client is a separate repository (<https://github.com/Ahmedsamyshafik/ECom-Angular>), checked out locally as a sibling folder in this workspace (`../FrontEnd_Src/Src/client`).

```bash
cd ../FrontEnd_Src/Src/client
npm install
npm start
```

`npm start` runs `ng serve`; the client is served at <http://localhost:4200>. The development environment points at `http://localhost:5090/api/v1`.

### Stripe webhook locally (optional)

Forward Stripe events to the API and copy the printed signing secret into `Stripe:WebhookSecret`:

```bash
stripe listen --forward-to http://localhost:5090/api/v1/Payment/Webhook
```

## Running tests

From the repository root:

```bash
dotnet test Src/ECom.Api/ECom.Api.slnx
```

The project contains unit tests (xUnit + Moq) and real-SQL integration tests.

### Real-SQL tests and `ECOM_SKIP_SQL_TESTS`

The integration tests in `StockReservationServiceSqlTests` need a running SQL Server at `localhost` (integrated security). They create and use the dedicated database `AngularDotNetEcom_ReservationTests` via `EnsureCreatedAsync`.

Set `ECOM_SKIP_SQL_TESTS=1` to skip them:

```powershell
# PowerShell
$env:ECOM_SKIP_SQL_TESTS = "1"
dotnet test Src/ECom.Api/ECom.Api.slnx
```

```bash
# bash
ECOM_SKIP_SQL_TESTS=1 dotnet test Src/ECom.Api/ECom.Api.slnx
```

Important: when `ECOM_SKIP_SQL_TESTS=1` (or SQL Server is unreachable), the SQL tests return early and xUnit counts them as **passed**. A green test run therefore does not by itself prove that the real-SQL tests executed; check the SQL Server availability if you need that guarantee.

## Known limitations / Roadmap

These are known and intentional for the current scope:

- Webhook event-level idempotency is not implemented (no event-id deduplication).
- Payments that arrive after the reservation expired are not refunded automatically; the failure is logged for manual refund.
- Confirmation of a multi-item order is not atomic: if one reservation fails mid-loop, earlier ones may already be confirmed.
- Checkout Session creation has no idempotency key, so repeated calls can create multiple payable sessions.
- Currency is hardcoded to USD.
- Success/cancel URLs, the auth cookie domain, and CORS are configured for `localhost` only.
- The guest cart is supported by the API but blocked in the Angular UI (the client requires sign-in before adding items).
- No admin UI.
- No dedicated order-history page (orders are fetched for the checkout success poll).
- No logout.
- No Angular tests.
- No CI pipeline.
- The Redis cart and the SQL reservation are not atomic (they are separate stores).
- Cart keys have no TTL.
