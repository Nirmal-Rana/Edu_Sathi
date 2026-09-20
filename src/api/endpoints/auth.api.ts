import { apiClient } from "./client";

type AuthResponse = {
  token: string;
  email: string;
  fullName: string;
};

export async function login(email: string, password: string): Promise<AuthResponse> {
  const { data } = await apiClient.post<AuthResponse>('/api/Auth/login', { email, password });
  return data;
}

export async function register(
  fullName: string,
  contact: string,
  email: string,
  password: string,
  confirmPassword: string
): Promise<AuthResponse> {
  const { data } = await apiClient.post<AuthResponse>('/api/Auth/register', {
    fullName,
    contact,
    email,
    password,
    confirmPassword,
  });
  return data;
}