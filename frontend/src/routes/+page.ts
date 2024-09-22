import type { RoomMember } from "$lib/api";
import type { PageLoad } from "./$types";

export const load: PageLoad = async ({ fetch }) => {
	let members: RoomMember[] = await fetch("/api/room/members").then(data => data.json());

	return {
		members: members
	}
}
