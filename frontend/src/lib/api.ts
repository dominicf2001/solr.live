export interface RoomMember {
  songQueue: Array<Media>;
  iD: string;
}

export interface Session {
  dJ: RoomMember;
  startTime: string;
  media: Media;
  likes: number;
  dislikes: number;
}

export interface Media {
  link: string;
  duration: string;
}
