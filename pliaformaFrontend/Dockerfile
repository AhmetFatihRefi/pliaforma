# ---- deps ----
FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

# ---- build ----
FROM node:22-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
# Dev/prod compose'ta backend servisine yönlenmesi için build arg.
ARG BACKEND_ORIGIN=http://backend:8080
ENV BACKEND_ORIGIN=$BACKEND_ORIGIN
# Same-origin: client bundle relative `/api` kullanır (boş bırakılır).
ENV NEXT_PUBLIC_API_URL=""
RUN npm run build

# ---- runtime (standalone) ----
FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
ENV PORT=3000
EXPOSE 3000
COPY --from=build /app/public ./public
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
ENV BACKEND_ORIGIN=http://backend:8080
CMD ["node", "server.js"]
