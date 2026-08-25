FROM node:22-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY tsconfig.json ./
COPY src/ ./src/
RUN npm run build

FROM node:22-alpine AS runtime
RUN apk add --no-cache font-noto font-dejavu font-freefont fontconfig
WORKDIR /app
COPY package*.json ./
RUN npm ci --omit=dev
COPY --from=builder /app/dist ./dist
COPY assets/ ./assets/

EXPOSE 8000
CMD ["node", "dist/index.js"]
