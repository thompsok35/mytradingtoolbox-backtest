/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        dark: {
          900: '#0B0F19',
          800: '#111827',
          700: '#1F2937',
          600: '#374151',
          500: '#4B5563'
        },
        brand: {
          500: '#3B82F6',
          600: '#2563EB',
          700: '#1D4ED8'
        },
        emerald: {
          400: '#34D399',
          500: '#10B981',
          600: '#059669'
        },
        rose: {
          400: '#FB7185',
          500: '#F43F5E',
          600: '#E11D48'
        }
      }
    },
  },
  plugins: [],
}
