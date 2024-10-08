import adapter from "svelte-adapter-bun";
import { vitePreprocess } from "@sveltejs/vite-plugin-svelte";

/** @type {import('@sveltejs/kit').Config} */
const config = {
  preprocess: [vitePreprocess({})],
  compilerOptions: {},
  kit: {
    adapter: adapter({
      out: "build",
      assets: true,
    }),
  },
};

export default config;
