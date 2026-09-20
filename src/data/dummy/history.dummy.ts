export type HistoryDoc = {
  id: string;
  name: string;
  date: string;
  pages: number;
  sizeMB: number;
  isCurrent?: boolean;
};

export const dummyHistory: HistoryDoc[] = [
  { id: '1', name: 'Junior_Developer_Assessment_Unmarked.pdf', date: 'Sep 18, 2026 · 2:30 PM', pages: 4, sizeMB: 0.0 },
  { id: '2', name: 'Industrial Management Assignment 20026-7dd30a5a-f6e8-4db...', date: 'Sep 18, 2026 · 2:29 PM', pages: 4, sizeMB: 0.3 },
  { id: '3', name: 'Introduction to Photosynthesis.pdf', date: 'Sep 16, 2026 · 2:57 PM', pages: 18, sizeMB: 1.2, isCurrent: true },
  { id: '4', name: 'Nepal Modern History – Chapter 4.pdf', date: 'Sep 14, 2026 · 9:25 PM', pages: 42, sizeMB: 2.8 },
  { id: '5', name: 'Calculus Limits & Continuity.pdf', date: 'Sep 11, 2026 · 12:50 PM', pages: 12, sizeMB: 1.0 },
];