import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { VitePWA } from "vite-plugin-pwa";

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
              expiration: { maxEntries: 100, maxAgeSeconds: 60 * 60 * 24 * 7 },
            },
          },
          // Шрифты — Cache First
          {
            urlPattern: ({ request }) => request.destination === "font",
            handler: "CacheFirst",
            options: {
              cacheName: "fonts",
              expiration: { maxEntries: 20, maxAgeSeconds: 60 * 60 * 24 * 365 },
            },
          },
          // Картинки — Cache First
          {
            urlPattern: ({ request }) => request.destination === "image",
            handler: "CacheFirst",
            options: {
              cacheName: "images",
              expiration: { maxEntries: 60, maxAgeSeconds: 60 * 60 * 24 * 30 },
            },
          },
          // Тайлы для карт (OSM / Carto) — Cache First, ограничение
          {
            urlPattern:
              /^(https:\/\/[abc]\.tile\.openstreetmap\.org\/|https:\/\/[a-d]\.basemaps\.cartocdn\.com\/)/,
            handler: "CacheFirst",
            options: {
              cacheName: "map-tiles",
              expiration: { maxEntries: 200, maxAgeSeconds: 60 * 60 * 24 * 3 },
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
    // Добавляем заголовки для поддержки iframe (Telegram WebApp)
    headers: {
      'X-Frame-Options': 'ALLOWALL',
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET,POST,PUT,DELETE,OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type,Authorization',
    },
  },
  
  // Настройки для SPA и iframe
  base: './',
  
  // Дополнительные настройки для iframe
  define: {
    global: 'globalThis',
  },
  
  // Настройки для продакшена
  preview: {
    headers: {
      'X-Frame-Options': 'ALLOWALL',
      'Content-Security-Policy': `
        default-src 'self';
        script-src 'self' 'unsafe-inline' 'unsafe-eval' https://telegram.org;
        img-src 'self' data: https:;
        connect-src 'self' https:;
        frame-ancestors *;
      `.replace(/\s+/g, ' ').trim(),
    },
  },
});