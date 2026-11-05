
# ==========================================
# ETAPA 1 - BUILD REACT
# ==========================================
FROM node:24-alpine AS build

WORKDIR /app

# Dependencias
COPY package*.json ./

RUN npm ci

# Codigo React
COPY . .

# Compilacao de producao
RUN npm run build

# ==========================================
# ETAPA 2 - SERVIDOR NGINX
# ==========================================
FROM nginx:alpine AS final

# Configuracao do Nginx
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copiar arquivos gerados pelo Vite
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

# Verificacao de funcionamento
HEALTHCHECK --interval=15s \
    --timeout=5s \
    --start-period=10s \
    --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1/ || exit 1
