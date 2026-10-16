import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

// Fixed time zone so local-time <-> UTC conversions are deterministic.
// Europe/Berlin is UTC+2 in summer (CEST) and UTC+1 in winter (CET).
process.env.TZ = "Europe/Berlin";

export default defineConfig({
  plugins: [react()],
  resolve: {
    // Resolves the "@/*" alias from tsconfig.json.
    tsconfigPaths: true,
  },
  test: {
    environment: "jsdom",
    include: ["tests/unit/**/*.test.{ts,tsx}"],
    setupFiles: ["./tests/unit/setup.ts"],
    restoreMocks: true,
    unstubGlobals: true,
  },
});
