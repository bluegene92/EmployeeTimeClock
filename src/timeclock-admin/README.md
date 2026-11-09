# TimeClock Admin

Next.js admin portal. It runs on your desktop and calls the hosted .NET API.

## Run locally

```bash
copy .env.local.example .env.local
```

Set `NEXT_PUBLIC_API_URL` to your Render API URL (no trailing slash), then:

```bash
npm install
npm run dev
```

Open [http://localhost:3000](http://localhost:3000). Log in with `admin` / `admin123`.

Full Render setup is in [DEPLOYMENT.md](../../DEPLOYMENT.md).
