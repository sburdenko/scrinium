# Getting started with Kestrel Analytics

This guide leads you from a new account to your first dashboard.

## The steps

1. Create an account and confirm your email address.
2. Add your first site by entering its domain.
3. Copy the tracking snippet that Kestrel shows you.
4. Paste the snippet into your website, directly before the closing `</head>` tag.
5. Verify the installation. The site status turns green and reads "receiving data" within 5 minutes of the first page view.
6. Configure goals, for example a visit to the thank-you page after a purchase.
7. Invite your team members. The number of seats depends on your plan.
8. Create your first dashboard from the templates.
9. Set up email alerts if your plan includes them (Team and Business).
10. Create an API key under Settings if you want to read data from your own systems.

## If no data arrives

Check that the snippet is on every page and that it appears before the closing `</head>` tag. Browser extensions that block trackers can prevent the script from loading; test in a private window without extensions.

If your website uses a Content Security Policy, add `https://cdn.kestrel.example` to the `script-src` directive and `https://api.kestrel.example` to the `connect-src` directive. Without these entries the browser blocks the script silently.

Single-page applications must send a page view on every route change. The snippet exposes `kestrel.pageview()` for this purpose.

## Inviting your team

Invited members receive an email with a link that is valid for 7 days. Roles are Viewer, Editor and Admin. Only Admins can change billing, create API keys and, on the Business plan, configure single sign-on.

## Moving from another tool

Historical data from other analytics tools can be imported as CSV files up to 100 MB each. Imported events count toward the monthly event limit of the month in which they are imported.
