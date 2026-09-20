import { apiClient } from './client';

export type DocumentDto = {
  id: number;
  name: string;
  pages: number;
  status?: 'pending' | 'processing' | 'completed' | 'failed';
  uploadDate?: string;
  fileSizeKb?: number;
};

export type DocumentSummary = {
  overview: string[];
  keyTakeaways: { label: string; value: string }[];
};

type ApiEnvelope<T> = {
  status: string;
  message: string;
  data: T;
};

export async function uploadDocument(uri: string, name: string): Promise<DocumentDto> {
  const formData = new FormData();
  formData.append('File', {
    uri,
    name,
    type: 'application/pdf',
  } as any);

  const { data } = await apiClient.post<ApiEnvelope<DocumentDto>>('/api/v1/Documents/upload', formData, {
    headers: {
      'Content-Type': 'multipart/form-data',
    },
  });

  return data.data;
}

export async function getDocuments(): Promise<DocumentDto[]> {
  const { data } = await apiClient.get<ApiEnvelope<DocumentDto[]>>('/api/v1/Documents');
  return data.data;
}

// Returns null if the summary isn't ready yet (status !== 'completed').
export async function getDocumentSummary(id: number): Promise<DocumentSummary | null> {
  try {
    const { data } = await apiClient.get<ApiEnvelope<DocumentSummary | null>>(
      `/api/v1/Documents/${id}/summary`
    );
    return data.data;
  } catch (err: any) {
    if (err?.response?.status === 404 || err?.response?.status === 202) {
      return null; // not ready yet
    }
    throw err;
  }
}