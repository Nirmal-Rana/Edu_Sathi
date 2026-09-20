import { apiClient } from './client';

export type Friend = {
  id: string;
  name: string;
  email: string;
};

type ApiEnvelope<T> = {
  status: string;
  message: string;
  data: T;
};

export async function getFriends(): Promise<Friend[]> {
  const { data } = await apiClient.get<ApiEnvelope<Friend[]>>('/api/v1/Friends');
  return data.data;
}

export async function addFriend(email: string): Promise<Friend> {
  const { data } = await apiClient.post<ApiEnvelope<Friend>>('/api/v1/Friends/add', {
    email,
  });
  return data.data;
}

export async function removeFriend(friendId: string): Promise<void> {
  await apiClient.delete(`/api/v1/Friends/${friendId}`);
}