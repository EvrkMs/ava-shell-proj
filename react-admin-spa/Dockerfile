# Multi-stage build: build React (Vite) app, then serve via Nginx

# 1) Build stage
FROM node:18-alpine AS build
WORKDIR /app

# Install dependencies first (better layer caching)
COPY package*.json ./
RUN npm ci --no-audit --no-fund

# Copy sources and build
COPY . .
RUN npm run build

# 2) Runtime stage with Nginx
FROM nginx:alpine AS runtime

# Copy custom Nginx config (SPA routing, listen 5000)
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy built assets
COPY --from=build /app/dist /usr/share/nginx/html

# Expose Nginx port
EXPOSE 5000

CMD ["nginx", "-g", "daemon off;"]

