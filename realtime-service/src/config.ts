import "dotenv/config";

export const config = {
  dotnetApiBaseUrl: process.env.DOTNET_API_BASE_URL ?? "http://localhost:5043",
  port: Number(process.env.PORT ?? 4000),
  corsOrigin: process.env.CORS_ORIGIN ?? "*",
};
