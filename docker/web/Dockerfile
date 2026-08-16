FROM node:22-alpine AS build
WORKDIR /app

ARG VITE_APP_MODE=full
ARG VITE_BASE_PATH=/
ARG VITE_API_URL=/api

ENV VITE_APP_MODE=$VITE_APP_MODE
ENV VITE_BASE_PATH=$VITE_BASE_PATH
ENV VITE_API_URL=$VITE_API_URL

COPY VibeTest/vibetest.client/package.json VibeTest/vibetest.client/package-lock.json ./
RUN npm ci

COPY VibeTest/vibetest.client/ ./
RUN npm run build

FROM nginx:1.27-alpine AS runtime

COPY docker/web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
