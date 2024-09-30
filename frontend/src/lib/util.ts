export function genSessionKey() {
	const array = new Uint32Array(10);
	window.crypto.getRandomValues(array);

	const randomPart = Array.from(array, num => num.toString(36)).join('');
	const timestamp = Date.now().toString(36);

	const sessionKey = `${timestamp}-${randomPart}`;

	return sessionKey;
}
