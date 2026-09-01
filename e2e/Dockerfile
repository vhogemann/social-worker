FROM mcr.microsoft.com/playwright:v1.62.0-jammy
WORKDIR /e2e
COPY package.json package-lock.json* ./
RUN --mount=type=cache,id=npm-e2e,target=/root/.npm \
    npm ci
COPY . .
CMD ["npx", "playwright", "test"]