export interface Room {
  hostQueue: Array<RoomMember>;
  session: Session | null;
  members: Record<string, RoomMember>;
  name: string;
  owner: string;
}

export interface RoomMember {
  mediaQueue: Array<Media>;
  id: string;
}

export interface Session {
  host: RoomMember;
  startTime: string;
  media: Media;
  likes: number;
  dislikes: number;
}

export interface Media {
  link: string;
  duration: string;
}
