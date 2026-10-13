# Kestrel Analytics API reference

The current API version is v2. The base URL is `https://api.kestrel.example/v2`. Version v1 is deprecated and will be shut down on 30 June 2026.

## Authentication

Every request carries an API key as a bearer token in the `Authorization` header. Create keys under Settings, then API keys. A key expires 12 months after it was created and must be replaced. Keys on the Free plan are read only: they can fetch statistics but cannot send events or delete data.

## Rate limits

Rate limits are applied per API key and per minute.

| Plan | Requests per minute |
|---|---|
| Free | 60 |
| Starter | 300 |
| Team | 1,200 |
| Business | 6,000 |

Short bursts of up to twice the limit are tolerated for 10 seconds. When the limit is exceeded the API answers with status 429 and a `Retry-After` header that tells the client how many seconds to wait. Clients should back off and must not retry in a tight loop.

## Pagination

List endpoints return 100 items per page by default. The `limit` parameter raises this up to a maximum of 500 items per page. To fetch the next page, pass the `cursor` value from the previous response. Offsets are not supported.

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/sites` | List the sites of the account |
| GET | `/sites/{id}/stats` | Visitors, page views and sources for a period |
| POST | `/events` | Send events; up to 1,000 events per request |
| GET | `/reports/{id}` | Fetch a saved report |
| DELETE | `/visitors/{id}` | Delete a visitor and all of their events |

The `POST /events` endpoint accepts a batch of up to 1,000 events in one request. Larger batches are rejected with status 413. Each event needs a timestamp in ISO 8601 format and a site identifier.

## Webhooks

Kestrel can notify your system when an alert fires or a report is ready. Every webhook request is signed with HMAC-SHA256 and carries the signature in the `X-Kestrel-Signature` header. Your server must verify the signature before it trusts the payload.

If your server does not answer with a status in the 2xx range, Kestrel retries the delivery up to 5 times. The waiting times between attempts are 1 second, 5 seconds, 30 seconds, 2 minutes and 10 minutes. After the fifth failed attempt the event is dropped and appears in the delivery log for 7 days.

## Errors

| Status | Meaning |
|---|---|
| 400 | The request is malformed or a parameter is invalid |
| 401 | The API key is missing, wrong or expired |
| 403 | The plan does not allow this operation, for example writing with a read only key |
| 404 | The resource does not exist |
| 413 | The batch is larger than 1,000 events |
| 429 | The rate limit was exceeded |
| 500 | An internal error occurred; retry later |
