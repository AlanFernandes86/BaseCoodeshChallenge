# Build stage
FROM node:20-alpine AS build
WORKDIR /app

COPY OrderGenerator.Frontend/package*.json ./
RUN npm install

COPY OrderGenerator.Frontend/ ./
RUN npm run build

# Runtime stage
FROM node:20-alpine
WORKDIR /app
RUN npm install -g serve

COPY --from=build /app/dist ./dist

EXPOSE 5173
CMD ["serve", "-s", "dist", "-l", "5173"]
