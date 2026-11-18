# syntax=docker/dockerfile:1

# Builds the Angular admin panel and serves it as static files.
# The API is no longer involved in serving the admin UI.

FROM node:22.12-alpine AS build
WORKDIR /src

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

FROM nginx:1-alpine AS final

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/UI/browser /usr/share/nginx/html

EXPOSE 80
