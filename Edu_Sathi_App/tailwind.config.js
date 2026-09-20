module.exports = {
  content: ['./App.tsx', './src/**/*.{js,jsx,ts,tsx}'],
  presets: [require('nativewind/preset')],
  theme: {
    extend: {
      colors: {
        primary: '#7C5CFC',
        accent: '#F97316',
        bg: '#F5F6FA',       // light background
        surface: '#FFFFFF',  // white cards/divs — slightly whiter than bg
      },
    },
  },
  plugins: [],
};