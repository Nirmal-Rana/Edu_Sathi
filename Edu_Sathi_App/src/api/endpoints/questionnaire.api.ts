import { apiClient } from './client';

export type QuizOption = {
  id: string;
  text: string;
};

export type QuizQuestion = {
  id: number;
  prompt: string;
  options: QuizOption[];
};

export type RoomParticipant = {
  id: string;
  name: string;
};

export type Room = {
  roomCode: string;
  roomName: string;
  ownerId: string;
  documentId: number;
  questionCount: number;
  participants: RoomParticipant[];
  started?: boolean;
};

export type QuizAnswer = {
  questionId: number;
  selectedOption: string;
};

export type QuizResult = {
  score: number;
  totalQuestions: number;
  correctCount: number;
};

type ApiEnvelope<T> = {
  status: string;
  message: string;
  data: T;
};

export async function createRoom(
  documentId: number,
  questionCount: number,
  roomName: string
): Promise<Room> {
  const { data } = await apiClient.post<ApiEnvelope<Room>>('/api/v1/Rooms/create', {
    documentId,
    questionCount,
    roomName,
  });
  return data.data;
}

export async function joinRoom(roomCode: string): Promise<Room> {
  const { data } = await apiClient.post<ApiEnvelope<Room>>('/api/v1/Rooms/join', {
    roomCode,
  });
  return data.data;
}

export async function getRoom(roomCode: string): Promise<Room> {
  const { data } = await apiClient.get<ApiEnvelope<Room>>(`/api/v1/Rooms/${roomCode}`);
  return data.data;
}

export async function inviteFriend(roomCode: string, friendId: string): Promise<void> {
  await apiClient.post('/api/v1/Rooms/invite', { roomCode, friendId });
}

export async function startRoom(roomCode: string): Promise<void> {
  await apiClient.post('/api/v1/Rooms/start', { roomCode });
}

export async function getQuizQuestions(roomCode: string): Promise<QuizQuestion[]> {
  const { data } = await apiClient.get<ApiEnvelope<QuizQuestion[]>>(
    `/api/v1/Quiz/${roomCode}/questions`
  );
  return data.data;
}

export async function submitQuiz(
  roomCode: string,
  answers: QuizAnswer[]
): Promise<QuizResult> {
  const { data } = await apiClient.post<ApiEnvelope<QuizResult>>('/api/v1/Quiz/submit', {
    roomCode,
    answers,
  });
  return data.data;
}