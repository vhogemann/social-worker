FROM mcr.microsoft.com/playwright:v1.52.0-jammy
WORKDIR /e2e
COPY package.json package-lock.json* ./
RUN npm ci
COPY . .
CMD ["npx", "playwright", "test"]