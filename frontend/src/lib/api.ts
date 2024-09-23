export interface RoomMember {
  songQueue: Array<Song>;
  iD: string;
}

export interface DJSession {
  dJ: RoomMember;
  startTime: string;
  song: Song;
  likes: number;
  dislikes: number;
}

export interface Song {
  link: string;
  duration: string;
}
