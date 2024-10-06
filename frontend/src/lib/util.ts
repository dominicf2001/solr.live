export function genSessionKey() {
	const array = new Uint32Array(10);
	window.crypto.getRandomValues(array);

	const randomPart = Array.from(array, num => num.toString(36)).join('');
	const timestamp = Date.now().toString(36);

	const sessionKey = `${timestamp}-${randomPart}`;

	return sessionKey;
}


export function scrollToBottom(node: HTMLElement | null) {
	if (!node) return;

	const scroll = () => node.scroll({
		top: node.scrollHeight,
		behavior: 'smooth',
	});
	scroll();

	return { update: scroll }
};

export function formatDate(dateStr: string): string {
	const date = new Date(dateStr);
	let hours = date.getHours();
	const minutes = date.getMinutes().toString().padStart(2, "0");
	const ampm = hours >= 12 ? "PM" : "AM";

	hours = hours % 12;
	hours = hours ? hours : 12;

	const formattedHours = hours.toString().padStart(2, "0");
	return `${formattedHours}:${minutes} ${ampm}`;
}
