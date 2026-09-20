import { useCallback, useState } from 'react';

import { generateQuiz, getSavedFiles, uploadFile, joinRoomByCode, SavedFile, QuizQuestion } from '@/api/endpoints/questionnaire.api';

export type Mode = 'solo' | 'custom';
export type Step = 'mode' | 'source' | 'upload' | 'saved' | 'quiz' | 'share';

const BACK: Record<Step, Step> = {
  mode: 'mode',
  source: 'mode',
  upload: 'source',
  saved: 'source',
  quiz: 'source',
  share: 'source',
};

export function useQuestionnaireFlow() {
  const [step, setStep] = useState<Step>('mode');
  const [mode, setMode] = useState<Mode | null>(null);
  const [savedFiles, setSavedFiles] = useState<SavedFile[]>([]);
  const [quiz, setQuiz] = useState<QuizQuestion[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const pickMode = useCallback((m: Mode) => {
    setMode(m);
    setStep('source');
  }, []);

  const goToUpload = useCallback(() => setStep('upload'), []);

  const goToSaved = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const files = await getSavedFiles();
      setSavedFiles(files);
      setStep('saved');
    } catch {
      setError('Could not load saved files.');
    } finally {
      setLoading(false);
    }
  }, []);

  const finishWithSource = useCallback(
    async (source: SavedFile) => {
      setLoading(true);
      setError(null);
      try {
        const questions = await generateQuiz(source);
        setQuiz(questions);
        setStep(mode === 'solo' ? 'quiz' : 'share');
      } catch {
        setError('Could not generate the questionnaire.');
      } finally {
        setLoading(false);
      }
    },
    [mode]
  );

  const confirmUpload = useCallback(
    async (uri: string) => {
      setLoading(true);
      setError(null);
      try {
        const file = await uploadFile(uri);
        await finishWithSource(file);
      } catch {
        setError('Could not upload the file.');
      } finally {
        setLoading(false);
      }
    },
    [finishWithSource]
  );

  const chooseSavedFile = useCallback(
    (file: SavedFile) => finishWithSource(file),
    [finishWithSource]
  );

  const joinRoom = useCallback(async (roomCode: string) => {
    if (!roomCode.trim()) return;
    setLoading(true);
    setError(null);
    try {
      await joinRoomByCode(roomCode.trim());
      // TODO: once SignalR contract is confirmed, connect to the hub here
      // and transition into a live "in room" quiz state instead of 'quiz'.
      setStep('quiz');
    } catch {
      setError('Invalid room code, or the room is no longer active.');
    } finally {
      setLoading(false);
    }
  }, []);

  const goBack = useCallback(() => {
    setStep((s) => BACK[s]);
  }, []);

  return {
    step,
    mode,
    savedFiles,
    quiz,
    loading,
    error,
    pickMode,
    goToUpload,
    goToSaved,
    confirmUpload,
    chooseSavedFile,
    goBack,
    joinRoom,
  };
}