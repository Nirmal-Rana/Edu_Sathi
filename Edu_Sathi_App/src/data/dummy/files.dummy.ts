export type SavedFile = {
  id: string;
  name: string;
  pages: number;
  sizeKb: number;
};

export const dummySavedFiles: SavedFile[] = [
  { id: 'f1', name: 'Junior_Developer_Assessment_Unmarked.pdf', pages: 4, sizeKb: 16 },
  { id: 'f2', name: 'Industrial Management Assignment.pdf', pages: 4, sizeKb: 264 },
  { id: 'f3', name: 'Introduction to Photosynthesis.pdf', pages: 18, sizeKb: 1240 },
  { id: 'f4', name: 'Nepal Modern History – Chapter 4.pdf', pages: 42, sizeKb: 2860 },
  { id: 'f5', name: 'Calculus Limits & Continuity.pdf', pages: 12, sizeKb: 980 },
  { id: 'f6', name: 'Cell Biology Lecture Notes.pdf', pages: 55, sizeKb: 3410 },
];