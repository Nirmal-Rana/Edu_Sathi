export const API_BASE_URL = process.env.EXPO_PUBLIC_API_BASE_URL ?? '';

if (!API_BASE_URL) {
  console.warn('EXPO_PUBLIC_API_BASE_URL is not set — check your .env file');
}