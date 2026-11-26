# CarShop Backend

ASP.NET Core API for the [CarShop frontend](https://github.com/SeinurKabilzhaparova/CarShopFrontend).

## Run locally

With Docker Compose and OpenSSL installed, run:

```sh
export CARSHOP_JWT_KEY="$(openssl rand -hex 32)"
docker compose up --build
```

The API listens on `http://localhost:5107`. Compose starts PostgreSQL and Redis and applies database migrations before the API starts. Reuse the same local key across restarts if existing login tokens should stay valid. You can also set `CARSHOP_JWT_KEY` in a local `.env` file ignored by Git.

Outside Compose, set `jwt__key` in the API environment to a private signing key of at least 32 UTF-8 bytes. Set `ConnectionStrings__DefaultConnection` and `Redis__ConnectionString` to the database and Redis endpoints for that environment. The checked-in Compose defaults are for local development.

`appsettings.json` contains shared defaults. ASP.NET Core also loads `appsettings.Development.json` when the environment is `Development`. Environment variables override both files. For nested keys, use two underscores in the variable name. For example, `jwt__key` sets `jwt:key`.

## Orders

Create your customer profile as an authenticated user before placing an order. `POST /api/order` uses that profile and requires no request body. Reading or cancelling a missing order or an order owned by another customer returns HTTP 404.

## Buying and selling from one account

Register with `email` and `password` at `POST /api/auth/register`. The same account can create a buyer profile for orders and a seller profile for listings. Older clients may still send a registration role. The API assigns its own default.

Create a personal seller profile at `POST /seller/private` using `name`, `city`, `phone`, and `email`. The existing `POST /seller` accepts business details. Both use the signed-in account and return the seller ID. Each account can have one seller profile. Look up your own profile at `GET /seller/me`.

`POST /api/listing` uses the current account's seller profile. Submit `carId`, `city`, `description`, and `photos` as multipart form data. Only the listing owner can edit or delete it. Configure moderator account IDs with `Moderation__UserIds__0`, `Moderation__UserIds__1`, and so on to enable approval and rejection.
