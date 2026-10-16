import type { NextConfig } from "next";

/**
 * The browser only ever calls relative `/api/v1/...` URLs on this origin.
 * Next.js proxies them to the backend so the `tm_session` cookie stays
 * first-party and no CORS is needed (SPEC 5.3 / 5.4).
 *
 * BACKEND_URL is a server/build-time variable (no NEXT_PUBLIC_ prefix), e.g.
 * `http://localhost:5000` locally or `https://<app>.azurewebsites.net` on Vercel.
 * The destination keeps the `/api` prefix because the backend routes are `/api/v1/...`.
 */
function resolveBackendUrl(): string {
  const raw = process.env.BACKEND_URL?.trim();
  if (!raw) {
    throw new Error(
      "BACKEND_URL is not set. Copy .env.example to .env.local (local dev) or set it in the Vercel project settings before building.",
    );
  }
  let parsed: URL;
  try {
    parsed = new URL(raw);
  } catch {
    throw new Error(`BACKEND_URL is not a valid absolute URL: "${raw}"`);
  }
  if (parsed.protocol !== "http:" && parsed.protocol !== "https:") {
    throw new Error(`BACKEND_URL must use http or https: "${raw}"`);
  }
  return raw.replace(/\/+$/, "");
}

const nextConfig: NextConfig = {
  async rewrites() {
    const backendUrl = resolveBackendUrl();
    return [
      {
        source: "/api/:path*",
        destination: `${backendUrl}/api/:path*`,
      },
    ];
  },
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
