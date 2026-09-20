import { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { login as loginApi, register as registerApi } from '../api/endpoints/auth.api';

type User = { email: string; fullName: string };

type AuthContextType = {
  user: User | null;
  token: string | null;
  isLoading: boolean;
  signIn: (email: string, password: string) => Promise<void>;
  signUp: (
    fullName: string,
    contact: string,
    email: string,
    password: string,
    confirmPassword: string
  ) => Promise<void>;
  signOut: () => Promise<void>;
};

const AuthContext = createContext<AuthContextType | null>(null);
const TOKEN_KEY = 'auth_token';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    AsyncStorage.getItem(TOKEN_KEY).then((stored) => {
      if (stored) setToken(stored);
      setIsLoading(false);
    });
  }, []);

  async function signIn(email: string, password: string) {
    const { token, email: userEmail, fullName } = await loginApi(email, password);
    await AsyncStorage.setItem(TOKEN_KEY, token);
    setToken(token);
    setUser({ email: userEmail, fullName });
  }

  async function signUp(
    fullName: string,
    contact: string,
    email: string,
    password: string,
    confirmPassword: string
  ) {
    const { token, email: userEmail, fullName: name } = await registerApi(
      fullName,
      contact,
      email,
      password,
      confirmPassword
    );
    await AsyncStorage.setItem(TOKEN_KEY, token);
    setToken(token);
    setUser({ email: userEmail, fullName: name });
  }

  async function signOut() {
    await AsyncStorage.removeItem(TOKEN_KEY);
    setToken(null);
    setUser(null);
  }

  return (
    <AuthContext.Provider value={{ user, token, isLoading, signIn, signUp, signOut }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}