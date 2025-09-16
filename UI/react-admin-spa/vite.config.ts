import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { VitePWA } from "vite-plugin-pwa";

// Build/version identifier for namespacing caches per deploy
const APP_VERSION =
  process.env.VITE_APP_VERSION ||
  process.env.GITHUB_SHA ||
  process.env.VERCEL_GIT_COMMIT_SHA ||
  new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14);

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: "autoUpdate",
      // Включить SW в dev для теста (можно выключить потом)
      devOptions: {
        enabled: true,           // true — SW будет работать на dev-сервере
        type: "module",
      },
      includeAssets: [
        "favicon.svg",
        "robots.txt",
        "apple-touch-icon.png",
      ],
      manifest: {
        name: "AVA Admin",
        short_name: "AVA Admin",
        start_url: "/",
        display: "standalone",
        background_color: "#111113",
        theme_color: "#111113",
        icons: [
          { src: "/pwa-192x192.png", sizes: "192x192", type: "image/png" },
          { src: "/pwa-512x512.png", sizes: "512x512", type: "image/png" },
        ],
      },
      workbox: {
        // Faster first load while SW is starting
        navigationPreload: true,
        // Clean up outdated precaches on new SW
        cleanupOutdatedCaches: true,
        // Activate the new SW immediately
        clientsClaim: true,
        skipWaiting: true,
        navigateFallback: "/index.html",
        runtimeCaching: [
          // JS/CSS чанки — SWR
          {
            urlPattern: ({ request }) =>
              request.destination === "script" ||
              request.destination === "style",
            handler: "StaleWhileRevalidate",
            options: {
              cacheName: "assets-swr",
              expiration: { maxEntries: 100, maxAgeSeconds: 60 * 60 * 24 * 7, purgeOnQuotaError: true },
            },
          },
          // Шрифты — Cache First
          {
            urlPattern: ({ request }) => request.destination === "font",
            handler: "CacheFirst",
            options: {
              cacheName: "fonts",
              expiration: { maxEntries: 20, maxAgeSeconds: 60 * 60 * 24 * 365, purgeOnQuotaError: true },
            },
          },
          // Картинки — Cache First
          {
            urlPattern: ({ request }) => request.destination === "image",
            handler: "CacheFirst",
            options: {
              cacheName: "images",
              expiration: { maxEntries: 60, maxAgeSeconds: 60 * 60 * 24 * 30, purgeOnQuotaError: true },
            },
          },
          // Тайлы для карт (OSM / Carto) — Cache First, ограничение
          {
            urlPattern:
              /^(https:\/\/[abc]\.tile\.openstreetmap\.org\/|https:\/\/[a-d]\.basemaps\.cartocdn\.com\/)/,
            handler: "CacheFirst",
            options: {
              cacheName: "map-tiles",
              expiration: { maxEntries: 200, maxAgeSeconds: 60 * 60 * 24 * 3, purgeOnQuotaError: true },
              cacheableResponse: { statuses: [0, 200] },
            },
          },
          // API (приватное) — не кэшируем
          { 
            urlPattern: /^https:\/\/auth\.ava-kk\.ru\/api\//, 
            handler: "NetworkOnly", 
            options: { cacheName: "api-no-cache" } 
          },

          // IDS /connect/* — не кэшируем
          { 
            urlPattern: /^https:\/\/auth\.ava-kk\.ru\/connect\//, 
            handler: "NetworkOnly", 
            options: { cacheName: "ids-no-cache" } 
          },

          // ⛔ Telegram widget — всегда с сети
          { 
            urlPattern: /^https:\/\/telegram\.org\/js\/telegram-web-app\.js/i, 
            handler: "NetworkOnly", 
            options: { cacheName: "tg-webapp-no-cache" } 
          },

          // ⛔ Telegram аватарки/ресурсы — всегда с сети (или просто не попадают под наши image-правила)
          { 
            urlPattern: /^https:\/\/t\.me\/i\/.*/i, 
            handler: "NetworkOnly", 
            options: { cacheName: "tg-img-no-cache" } 
          },
        ],
      },
    }),
  ],
  server: {
    host: true,
    port: 5173,
    strictPort: true,
    allowedHosts: ["admin.ava-kk.ru"],
    hmr: {
      host: "admin.ava-kk.ru",
      clientPort: 443, // если front открыт по HTTPS
      protocol: "wss",
      path: "/hmr",
    },
  },
  
  // Настройки для SPA и iframe
  base: './',
  
  // Дополнительные настройки для iframe
  define: {
    global: 'globalThis',
    __APP_VERSION__: JSON.stringify(APP_VERSION),
  },
});
