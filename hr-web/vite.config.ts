import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: "jsdom",
    setupFiles: ["./src/__tests__/setup.ts"],
    coverage: {
      provider: "v8",
      reporter: [
        "text",
        "html",
        "lcov",
        "json",
        "json-summary",
        "cobertura",
        "clover",
      ],
      include: ["src/**/*.{ts,tsx}"],
      exclude: [
        "src/main.tsx",
        "src/vite-env.d.ts",
        "src/types/index.ts",
        "src/__tests__/**",
      ],
      thresholds: {
        lines: 80,
        branches: 80,
        functions: 80,
        statements: 80,
      },
    },
  },
  server: {
    port: 3000,
    proxy: {
      "/api/hr": {
        target: "http://localhost:5001",
        changeOrigin: true,
      },
    },
  },
});
