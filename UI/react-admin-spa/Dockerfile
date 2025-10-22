# Multi-stage build: build React (Vite) app, then serve via Nginx

# 1) Dependencies + tests/build
FROM node:24-alpine AS base
WORKDIR /app

# Install dependencies first (better layer caching)
COPY package*.json ./
RUN npm ci --no-audit --no-fund

COPY . .

# Run quality checks / build (used by CI via --target test)
FROM base AS test
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
COPY --from=test /app/dist /usr/share/nginx/html

EXPOSE 5000
CMD ["nginx", "-g", "daemon off;"]
