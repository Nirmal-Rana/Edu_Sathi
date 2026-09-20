export type QuizQuestion = {
  id: string;
  prompt: string;
  options: { id: string; label: string; text: string }[];
};

export const dummyQuiz: QuizQuestion[] = [
  {
    id: 'q1',
    prompt: 'What are the products of the light-dependent reactions?',
    options: [
      { id: 'a', label: 'A', text: 'Glucose and O₂' },
      { id: 'b', label: 'B', text: 'ATP, NADPH and O₂' },
      { id: 'c', label: 'C', text: 'CO₂ and H₂O' },
      { id: 'd', label: 'D', text: 'ATP and glucose' },
    ],
  },
  {
    id: 'q2',
    prompt: 'In which part of the chloroplast does the Calvin cycle occur?',
    options: [
      { id: 'a', label: 'A', text: 'Thylakoid lumen' },
      { id: 'b', label: 'B', text: 'Outer membrane' },
      { id: 'c', label: 'C', text: 'Stroma' },
      { id: 'd', label: 'D', text: 'Granum' },
    ],
  },
  {
    id: 'q3',
    prompt: 'Which molecule is split to release oxygen during photosynthesis?',
    options: [
      { id: 'a', label: 'A', text: 'Glucose' },
      { id: 'b', label: 'B', text: 'Carbon dioxide' },
      { id: 'c', label: 'C', text: 'Water' },
      { id: 'd', label: 'D', text: 'ATP' },
    ],
  },
];