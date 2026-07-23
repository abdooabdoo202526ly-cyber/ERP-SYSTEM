# =============================================================================
# ERP Frontend - Dockerfile
# Pre-installs node_modules in the image so startup is fast (no npm install
# on every container start). Source code is mounted as a volume for live reload.
# =============================================================================

FROM node:20-alpine

WORKDIR /app

# Install deps first (cached as a separate layer so source code changes don't
# trigger a full npm install on every container start)
COPY package.json package-lock.json* ./
RUN npm ci --prefer-offline --no-audit --progress=false || npm install --prefer-offline --no-audit --progress=false

# Copy source (will be overridden by volume mount at runtime for live reload)
COPY . .

EXPOSE 3000

# Start Next.js dev server
CMD ["npm", "run", "dev"]
