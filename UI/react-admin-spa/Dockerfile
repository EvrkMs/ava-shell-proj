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

# Install OpenSSL
RUN apk add --no-cache openssl

# Generate self-signed SSL (valid 10 years)
RUN mkdir -p /etc/ssl/certs && \
    openssl req -x509 -nodes -days 3650 \
      -subj "/C=RU/ST=Moscow/L=Moscow/O=LocalDev/OU=Admin/CN=admin.ava-kk.ru" \
      -newkey rsa:2048 \
      -keyout /etc/ssl/certs/privkey.pem \
      -out /etc/ssl/certs/fullchain.pem

# Copy Nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy built app
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 5000
CMD ["nginx", "-g", "daemon off;"]

