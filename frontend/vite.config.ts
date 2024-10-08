import { sveltekit } from '@sveltejs/kit/vite';
import { vite as vidstack } from "vidstack/plugins";
import { defineConfig } from 'vite';

export default defineConfig({
	plugins: [vidstack(), sveltekit()],
	server: {
		proxy: {
			"/roomHub": {
				target: "http://localhost:5066",
				changeOrigin: true,
			}
		}
	}
});
