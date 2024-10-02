import type { Config } from 'tailwindcss';
import flowbitePlugin from 'flowbite/plugin';

export default {
  content: [
    './src/**/*.{html,js,svelte,ts}', 
    './node_modules/flowbite-svelte/**/*.{html,js,svelte,ts}',
    './node_modules/flowbite-svelte-icons/**/*.{html,js,svelte,ts}',
  ],
  darkMode: 'class',
  theme: {
    extend: {
    fontFamily: {
        comfy: ['Nunito', 'sans-serif'],
      },
      colors: {
        primary: {
          DEFAULT: '#4B2C34', 
          light: '#7D5A50',
          dark: '#3B2F2F',
        },
        secondary: {
          DEFAULT: '#2C3E50',
          light: '#3E2C50', 
          dark: '#2C3E37', 
        },
        tertiary: {
          DEFAULT: '#4B4E6D', 
          light: '#544E4C',
        },
        background: {
          light: '#3D4D41', 
          dark: '#2D2D2D',
          darker: '#262626'
        },
      },
    },
  },
  plugins: [flowbitePlugin],
} as Config;
