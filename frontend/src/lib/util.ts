export function generateSessionID() {
	const timestamp = Date.now().toString(36);
	const randomPart = Math.random().toString(36).substring(2, 15);
	return `${timestamp}-${randomPart}`;
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
