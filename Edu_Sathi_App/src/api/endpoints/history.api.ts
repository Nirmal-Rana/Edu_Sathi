import { apiClient } from './client';

export type HistoryDoc = {
  id: string;
  name: string;
  date: string;
  pages: number;
  sizeMB: number;
  isCurrent?: boolean;
};

type ApiEnvelope<T> = {
  status: string;
  message: string;
  data: T;
};

export async function getHistory(): Promise<HistoryDoc[]> {
  const { data } = await apiClient.get<ApiEnvelope<HistoryDoc[]>>('/api/v1/History');
  return data.data;
}